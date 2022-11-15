using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 基于 WebSocket 的远程调试工具客户端。
    /// </summary>
    public partial class RemoteClient : BaseClient
    {
        static JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        static RemoteClient()
        {
            var enumTypes = typeof(RemoteClient)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.PropertyType)
                .Where(x => typeof(BaseDomain).IsAssignableFrom(x))
                .SelectMany(x => x.GetNestedTypes())
                .Where(x => x.IsEnum)
                .Where(x => x.GetFields().Any(y => y.GetCustomAttribute<EnumValueAttribute>() != null))
                .ToArray();

            foreach (var enumType in enumTypes)
            {
                JsonSerializerOptions.Converters.Add(
                    (JsonConverter)Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(enumType))!
                    );
            }
        }

        readonly Uri uri;
        readonly int bufferSize;

        readonly ClientWebSocket client;
        readonly ConcurrentDictionary<int, WaitingMessageCallback> waitingMessages;
        readonly IdMaker idMaker;

        CancellationTokenSource? receiveMessagesCancellationTokenSource;
        Task? receiveMessagesTask;
        Exception? receiveMessagesException;

        /// <summary>
        /// 当接收到未知的消息时触发。
        /// </summary>
        public event UnknownMessageReceivedEventHandler? UnknownMessageReceived;

        /// <summary>
        /// 获取内部 WebSocket 客户端。
        /// </summary>
        public ClientWebSocket WebSocketClient => client;

        /// <summary>
        /// 获取内部 Uri 地址。
        /// </summary>
        public Uri Uri => uri;

        /// <summary>
        /// 初始化 基于 WebSocket 的远程调试工具客户端。
        /// </summary>
        /// <param name="uri">WebSocket 路径</param>
        /// <param name="bufferSize">可缓存区大小</param>
        public RemoteClient(Uri uri, int bufferSize = 4096)
        {
            this.uri = uri;
            this.bufferSize = bufferSize;

            client = new();
            waitingMessages = new();
            idMaker = new();
        }

        private async ValueTask ReceiveBigMessagesAsync(byte[] buffer, int receivedCount, CancellationToken cancellationToken = default)
        {
            var tempBuffer = RentBuffer(buffer.Length * 3);
            var tempBufferLength = 0;

            try
            {
                Array.Copy(buffer, 0, tempBuffer, tempBufferLength, receivedCount);

                tempBufferLength += receivedCount;

            Loop:

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (tempBuffer.Length - tempBufferLength < bufferSize)
                {
                    ResizeBuffer(ref tempBuffer, tempBuffer.Length * 2);
                }

                var receiveResult = await client.ReceiveAsync(
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                    tempBuffer.AsMemory(tempBufferLength),
#else
                    new ArraySegment<byte>(tempBuffer, tempBufferLength, tempBuffer.Length - tempBufferLength),
#endif
                    cancellationToken
                );

                tempBufferLength += receiveResult.Count;

                if (!receiveResult.EndOfMessage)
                {
                    goto Loop;
                }

                ReceivedMessageHandler(tempBuffer.AsSpan(0, tempBufferLength));
            }
            finally
            {
                ReturnBuffer(tempBuffer, tempBufferLength);
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(1, cancellationToken);

                var buffer = new byte[bufferSize];

                while (client.State is WebSocketState.Connecting or WebSocketState.Open)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var receiveResult = await client.ReceiveAsync(
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                        buffer.AsMemory(),
#else
                        new ArraySegment<byte>(buffer),
#endif
                        cancellationToken
                        );

                    if (receiveResult.EndOfMessage)
                    {
                        ReceivedMessageHandler(buffer.AsSpan(0, receiveResult.Count));
                    }
                    else
                    {
                        await ReceiveBigMessagesAsync(buffer, receiveResult.Count, cancellationToken);
                    }
                }
            }
            catch (Exception e)
            {
                ReceiveMessagesExceptionHandler(e);
            }
        }

        private void ReceivedMessageHandler(Span<byte> messageBytes)
        {
            var messageBasic = DeserializeMessage<MessageBasic>(messageBytes);

            if (messageBasic != null)
            {
                if (messageBasic.Id.HasValue)
                {
                    if (waitingMessages.TryRemove(messageBasic.Id.Value, out var waitingMessageCallback))
                    {
                        waitingMessageCallback(messageBytes);

                        return;
                    }
                }

                if (!string.IsNullOrEmpty(messageBasic.Method))
                {
                    var dotIndex = messageBasic.Method.IndexOf(".");

                    if (dotIndex != -1)
                    {
                        var domainName = messageBasic.Method.Substring(0, dotIndex);
                        var eventName = messageBasic.Method.Substring(dotIndex + 1);

                        RaiseEvent(domainName, eventName, messageBytes);

                        return;
                    }
                }
            }


            UnknownMessageReceived?.Invoke(messageBytes);
        }

        private void ReceiveMessagesExceptionHandler(Exception e)
        {
            receiveMessagesException = e;

            client.Abort();

            Task.Delay(128).ContinueWith(_ =>
            {
                var waitingMessages = this.waitingMessages.Values.ToList();

                this.waitingMessages.Clear();

                if (waitingMessages.Count != 0)
                {
                    foreach (var waitingMessage in waitingMessages)
                    {
                        waitingMessage(default);
                    }
                }
            });
        }

        /// <summary>
        /// 等待指定消息的响应消息。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="messageId">消息 Id</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回响应消息</returns>
        public async ValueTask<ResponseMessage<TResult>> WaitResponseMessageAsync<TResult>(int messageId, CancellationToken cancellationToken = default)
        {
            if (receiveMessagesException != null)
            {
                throw receiveMessagesException;
            }

            var taskCompletionSource = new TaskCompletionSource<ResponseMessage<TResult>>();

            waitingMessages[messageId] = (messageBytes) =>
            {
                if (receiveMessagesException != null)
                {
                    taskCompletionSource.TrySetException(receiveMessagesException);

                    return;
                }

                try
                {
                    var message = DeserializeMessage<ResponseMessage<TResult>>(messageBytes);

                    taskCompletionSource.TrySetResult(message);
                }
                catch (Exception e)
                {
                    taskCompletionSource.TrySetException(e);
                }
            };

            try
            {
                var responseMessage = await taskCompletionSource.Task.WaitAsync(cancellationToken);

                return responseMessage;
            }
            finally
            {
                waitingMessages.TryRemove(messageId, out _);
            }
        }

        /// <summary>
        /// 等待指定消息的响应结果。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="messageId">消息 Id</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回响应结果</returns>
        /// <exception cref="ChromeErrorException">Chrome 错误信息</exception>
        public async ValueTask<TResult> WaitResponseResultAsync<TResult>(int messageId, CancellationToken cancellationToken = default)
        {
            var responseMessage = await WaitResponseMessageAsync<TResult>(messageId, cancellationToken);

            if (responseMessage.Error != null)
            {
                throw new ChromeErrorException(responseMessage.Error);
            }

            Debug.Assert(responseMessage.Result != null);

            return responseMessage.Result;
        }

        /// <summary>
        /// 发送一个请求消息。
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <param name="requestMessage">请求消息</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        public async ValueTask SendRequestMessageAsync<TParams>(RequestMessage<TParams> requestMessage, CancellationToken cancellationToken = default)
        {
            if (receiveMessagesException != null)
            {
                throw receiveMessagesException;
            }

            var messageBytes = JsonSerializer.SerializeToUtf8Bytes(
                requestMessage,
                JsonSerializerOptions
                );

            await client.SendAsync(
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                messageBytes.AsMemory(),
#else
                new ArraySegment<byte>(messageBytes),
#endif
                WebSocketMessageType.Text,
                true,
                cancellationToken
                );
        }

        /// <summary>
        /// 发送一个请求消息。
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <param name="messageId">消息 Id</param>
        /// <param name="method">方法名</param>
        /// <param name="params">消息参数</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        public async ValueTask SendRequestMessageAsync<TParams>(int messageId, string method, TParams @params, CancellationToken cancellationToken = default)
        {
            await SendRequestMessageAsync(new RequestMessage<TParams>(messageId, method, @params), cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<TResult> SendRequestMessageAndWaitResponseResult<TParams, TResult>(string method, TParams @params, CancellationToken cancellationToken = default)
        {
            var messageId = idMaker.MakeId();

            var resultTask = WaitResponseResultAsync<TResult>(messageId, cancellationToken);

            await SendRequestMessageAsync(messageId, method, @params, cancellationToken);

            return await resultTask;
        }

        /// <inheritdoc/>
        public override async ValueTask<TResult> SendRequestMessageAndWaitResponseResult<TParams, TResult>(IMethodParams<TParams, TResult> @params, CancellationToken cancellationToken = default)
        {
            return await SendRequestMessageAndWaitResponseResult<TParams, TResult>(@params.GetMethod(), (TParams)@params, cancellationToken);
        }

        /// <summary>
        /// 连接到 Chrome。
        /// </summary>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <exception cref="InvalidOperationException">已经在连接中</exception>
        public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (receiveMessagesTask != null)
            {
                throw new InvalidOperationException(/* 请先关闭连接 */);
            }

            await client.ConnectAsync(uri, cancellationToken);

            receiveMessagesCancellationTokenSource = new CancellationTokenSource();
            receiveMessagesTask = ReceiveMessagesAsync(receiveMessagesCancellationTokenSource.Token);
        }

        /// <summary>
        /// 关闭连接。
        /// </summary>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <exception cref="InvalidOperationException">未连接到 Chrome</exception>
        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            if (receiveMessagesTask == null)
            {
                throw new InvalidOperationException(/* 请先打开连接 */);
            }

            Debug.Assert(receiveMessagesCancellationTokenSource != null);

            receiveMessagesCancellationTokenSource.Cancel();

            await receiveMessagesTask;

            receiveMessagesTask = null;
            receiveMessagesCancellationTokenSource = null;

            await client.CloseAsync(WebSocketCloseStatus.Empty, null, cancellationToken);
        }

        /// <inheritdoc/>
        internal protected override TMessage DeserializeMessage<TMessage>(Span<byte> messageBytes)
        {
            return JsonSerializer.Deserialize<TMessage>(messageBytes, JsonSerializerOptions)!;
        }

        /// <inheritdoc/>
        internal protected override Memory<byte> SerializeMessage<TMessage>(TMessage message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions);
        }

        /// <summary>
        /// 租借一个临时缓存区。
        /// </summary>
        /// <param name="minimumLength">缓存区最小长度</param>
        /// <returns>返回缓存区</returns>
        protected virtual byte[] RentBuffer(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        /// <summary>
        /// 修改临时缓存区大小。
        /// </summary>
        /// <param name="buffer">缓存区引用</param>
        /// <param name="newMinimumLength">缓存区新的最小长度</param>
        protected virtual void ResizeBuffer(ref byte[] buffer, int newMinimumLength)
        {
            var newBuffer = RentBuffer(newMinimumLength);

            Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);

            ArrayPool<byte>.Shared.Return(buffer);

            buffer = newBuffer;
        }

        /// <summary>
        /// 归还临时缓存区。
        /// </summary>
        /// <param name="buffer">缓存区</param>
        /// <param name="usedLength">缓存区已使用的长度</param>
        protected virtual void ReturnBuffer(byte[] buffer, int usedLength)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        /// <summary>
        /// 创建一个页面标签远程调试工具客户端。
        /// </summary>
        /// <param name="targetId">页面标签 Id</param>
        /// <param name="bufferSize">客户端缓存区大小</param>
        /// <returns>返回远程调试工具客户端</returns>
        public virtual RemoteClient CreateTargetClient(string targetId, int? bufferSize = null)
        {
            var targetWsUri = new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}/devtools/page/{targetId}");

            return new RemoteClient(targetWsUri, bufferSize ?? this.bufferSize);
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">是否是否托管资源</param>
        protected override void Dispose(bool disposing)
        {
            receiveMessagesCancellationTokenSource?.Cancel();

            client.Dispose();
        }

        /// <summary>
        /// 是否非托管资源。
        /// </summary>
        ~RemoteClient()
        {
            Dispose(false);
        }
    }

    delegate void WaitingMessageCallback(Span<byte> messageBytes);

    /// <summary>
    /// 接收到未知的消息事件的委托。
    /// </summary>
    /// <param name="messageBytes">消息字节码</param>
    public delegate void UnknownMessageReceivedEventHandler(Span<byte> messageBytes);
}