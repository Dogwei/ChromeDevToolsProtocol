namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// Chrome 调试工具客户端基类。提供除交互协议之外的所有信息。
    /// </summary>
    public abstract partial class BaseClient : IDisposable
    {
        /// <summary>
        /// 当未知的事件被触发是触发。
        /// </summary>
        public event UnknownEventRaisedEventHandler? UnknownEventRaised;

        /// <summary>
        /// 触发未知的事件。
        /// </summary>
        /// <param name="domainName">域名</param>
        /// <param name="eventName">事件名</param>
        /// <param name="messageBytes">事件消息字节码</param>
        public void RaiseUnknownEvent(string domainName, string eventName, Span<byte> messageBytes)
        {
            UnknownEventRaised?.Invoke(domainName, eventName, messageBytes);
        }

        /// <summary>
        /// 触发事件。
        /// </summary>
        /// <param name="domainName">事件所属域</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="messageBytes">消息体</param>
        public /* 由代码生成器实现 */ partial void RaiseEvent(string domainName, string eventName, Span<byte> messageBytes);

        /// <summary>
        /// 发送请求消息并等待响应结果。
        /// </summary>
        /// <typeparam name="TParams">消息参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="method">方法名</param>
        /// <param name="params">消息参数</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回响应结果</returns>
        public abstract ValueTask<TResult> SendRequestMessageAndWaitResponseResult<TParams, TResult>(
            string method,
            TParams @params,
            CancellationToken cancellationToken = default
            );

        /// <summary>
        /// 发送请求消息并等待响应结果。
        /// </summary>
        /// <typeparam name="TParams">消息参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="params">消息参数</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回响应结果</returns>
        public abstract ValueTask<TResult> SendRequestMessageAndWaitResponseResult<TParams, TResult>(
            IMethodParams<TParams, TResult> @params,
            CancellationToken cancellationToken = default
            ) where TParams : IMethodParams<TParams, TResult>;

        /// <summary>
        /// 反序列化消息。
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="messageBytes">消息字节码</param>
        /// <returns>返回消息</returns>
        internal protected abstract TMessage? DeserializeMessage<TMessage>(Span<byte> messageBytes);

        /// <summary>
        /// 序列化消息。
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="message">消息</param>
        /// <returns>返回消息字节码</returns>
        internal protected abstract Memory<byte> SerializeMessage<TMessage>(TMessage message);

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">是否是否托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// 释放所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 未知事件被触发的事件委托。
    /// </summary>
    /// <param name="domainName">域名</param>
    /// <param name="eventName">事件名</param>
    /// <param name="messageBytes">事件消息</param>
    public delegate void UnknownEventRaisedEventHandler(string domainName, string eventName, Span<byte> messageBytes);
}