using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 工具方法集。
    /// </summary>
    public static class Helper
    {
        internal const string HeadlessArgName = "--headless";
        internal const string UserDataDirArgName = "--user-data-dir";
        internal const string RemoteDebuggingPortName = "--remote-debugging-port";

        /// <summary>
        /// 调试工具端口文件的正则表达式，通常无需修改。
        /// </summary>
        public static Regex DevToolsActivePortRegex { get; set; } = new Regex(@"(?<Port>[0-9]+)[\r\n\s\t]*(?<Uri>[A-Za-z0-9_\-\\/]+)[\r\n\s\t]*$");

        /// <summary>
        /// 通过指定的远程调试端口获取调试监听路径。
        /// </summary>
        /// <param name="remoteDebuggingPort">指定的远程调试端口</param>
        /// <param name="scheme">协议，通用无需修改</param>
        /// <param name="host">主机，通常无需修改</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns></returns>
        public static async Task<string> GetListeningUriFromRemoteDebuggingPort(
            int remoteDebuggingPort,
            string scheme = "http",
            string host = "127.0.0.1",
            CancellationToken cancellationToken = default
            )
        {
            using HttpClient client = new HttpClient();

            var response = await client.GetAsync($"{scheme}://{host}:{remoteDebuggingPort}/json/version", cancellationToken);

           var versionJsonBytes = await response.Content.ReadAsByteArrayAsync(

#if NET5_0_OR_GREATER
               cancellationToken
#endif
               );

            var browserVersionInfo = JsonSerializer.Deserialize<BrowserVersionInfo>(versionJsonBytes);

            if (browserVersionInfo is null || browserVersionInfo.WebSocketDebuggerUrl is null)
            {
                throw new NotSupportedException("The current browser version may not support this protocol.");
            }

            return browserVersionInfo.WebSocketDebuggerUrl;
        }

        /// <summary>
        /// 在用户数据目录中获取远程调试监听路径。
        /// </summary>
        /// <param name="userDataDir">用户数据目录</param>
        /// <param name="devToolsActivePortFileName">端口记录文件，通常无需修改</param>
        /// <param name="scheme">协议，通用无需修改</param>
        /// <param name="host">主机，通常无需修改</param>
        /// <param name="deleteDevToolsActivePortFile">在获取到记录文件后会删除它</param>
        /// <param name="millisecondsTimeout">可设置超时时间</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException">超时异常</exception>
        public static async Task<string> GetListeningUriFromUserDataDirAsync(
            string userDataDir,
            string devToolsActivePortFileName = "DevToolsActivePort",
            string scheme = "ws",
            string host = "127.0.0.1",
            bool deleteDevToolsActivePortFile = true,
            long millisecondsTimeout = 10 * 1000
            )
        {
            var devToolsActivePortFullFileName = Path.Combine(userDataDir, devToolsActivePortFileName);

            var stopwatch = Stopwatch.StartNew();

            while (!File.Exists(devToolsActivePortFullFileName))
            {
                if (millisecondsTimeout >= 0 && stopwatch.ElapsedMilliseconds >= millisecondsTimeout)
                {
                    throw new TimeoutException();
                }

                await Task.Delay((int)Math.Sqrt(millisecondsTimeout));
            }

            try
            {
                var text =
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                    await File.ReadAllTextAsync(devToolsActivePortFullFileName);
#else
                    File.ReadAllText(devToolsActivePortFullFileName);
#endif

                var match = DevToolsActivePortRegex.Match(text);

                var port = match.Groups["Port"].Value;
                var uri = match.Groups["Uri"].Value;

                return $"{scheme}://{host}:{port}{uri}";
            }
            finally
            {
                if (deleteDevToolsActivePortFile)
                {
                    File.Delete(devToolsActivePortFullFileName);
                }
            }
        }

        /// <summary>
        /// 为进程的远程调试客户端创建新页面标签并创建页面标签的远程调试客户端。
        /// </summary>
        /// <param name="baseClient">进程的远程调试客户端</param>
        /// <param name="params">创建新页面标签的参数</param>
        /// <param name="bufferSize">可设置客户端的缓存区大小</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回页面标签的远程调试客户端</returns>
        public static async ValueTask<RemoteClient> CreateTargetClientAsync(
            this RemoteClient baseClient,
            TargetDomain.CreateTargetParams @params,
            int bufferSize = 4096,
            CancellationToken cancellationToken = default
            )
        {
            var createTargetResult = await baseClient.Target.CreateTargetAsync(@params, cancellationToken);

            return baseClient.CreateTargetClient(createTargetResult.TargetId, bufferSize);
        }

        /// <summary>
        /// 自动获取远程调试监听路径。
        /// </summary>
        /// <param name="chromeProcess">Chrome 进程</param>
        /// <param name="millisecondsTimeout">可设置超时时间</param>
        /// <returns>返回远程调试监听路径</returns>
        /// <exception cref="NotSupportedException">仅支持无头模式或指定了用户缓存目录的进程</exception>
        public static async ValueTask<string> AutoGetListeningUriAsync(
            this ChromeProcess chromeProcess,
            long millisecondsTimeout = 10 * 1000
            )
        {
            var remoteDebuggingPort = chromeProcess.GetArgValue(RemoteDebuggingPortName);

            if (string.IsNullOrEmpty(remoteDebuggingPort))
            {
                throw new NotSupportedException("The --remote-debugging-port argument must be specified.");
            }

            if (int.TryParse(remoteDebuggingPort, out var intRemoteDebuggingPort) && intRemoteDebuggingPort != 0)
            {
                return await GetListeningUriFromRemoteDebuggingPort(intRemoteDebuggingPort, cancellationToken: new CancellationTokenSource(checked((int)millisecondsTimeout)).Token);
            }

            return chromeProcess.HasArg(HeadlessArgName) ? await chromeProcess.GetListeningUriFromStandardErrorAsync(cancellationToken: new CancellationTokenSource(checked((int)millisecondsTimeout)).Token)
                : chromeProcess.GetArgValue(UserDataDirArgName) is string userDataDir ? await GetListeningUriFromUserDataDirAsync(userDataDir, millisecondsTimeout: millisecondsTimeout)
                : throw new NotSupportedException("Only supports process is --headless mode or with --user_data_dir specified.");
        }

        /// <summary>
        /// 为 Chrome 进程创建远程调试客户端。
        /// </summary>
        /// <param name="chromeProcess">Chrome 进程</param>
        /// <param name="bufferSize">可设置客户端的缓存区大小</param>
        /// <param name="millisecondsTimeout">可设置创建过程的超时时间</param>
        /// <returns>返回远程调试客户端</returns>
        /// <exception cref="NotSupportedException">仅支持无头模式或指定了用户缓存目录的进程</exception>
        public static async ValueTask<RemoteClient> CreateRemoteClientAsync(
            this ChromeProcess chromeProcess,
            int bufferSize = 4096,
            long millisecondsTimeout = 10 * 1000
            )
        {
            var listeningUri = await AutoGetListeningUriAsync(chromeProcess, millisecondsTimeout);

            return new RemoteClient(new Uri(listeningUri), bufferSize);
        }

        /// <summary>
        /// 判断一个日志是否为错误日志。
        /// </summary>
        public static bool IsErrorLog(this ChromeLogInfo chromeLogInfo)
        {
            switch (chromeLogInfo.LogLevel)
            {
                case "ERROR": return true;
            }

            return false;
        }

        /// <summary>
        /// 解析一个 Chrome 命令行参数。
        /// </summary>
        /// <param name="argText">参数文本</param>
        /// <param name="parsedLength">返回已解析的文本长度</param>
        /// <returns>返回参数值</returns>
        public static string ParseArg(ReadOnlySpan<char> argText, out int parsedLength)
        {
            int offset = 0;

            int length = 0;

            bool isInText = false;

            for (; offset < argText.Length; ++offset)
            {
                var chr = argText[offset];

                if (!isInText && char.IsWhiteSpace(chr))
                {
                    break;
                }

                if (chr is '"' && !isInText)
                {
                    isInText = true;

                    continue;
                }

                if (isInText && chr is '"' or '\\' && offset + 1 < argText.Length && argText[offset + 1] is '"')
                {
                    ++length;
                    ++offset;

                    continue;
                }

                if (chr is '"')
                {
                    isInText = false;

                    continue;
                }

                ++length;
            }

            parsedLength = offset;

            var chars = new char[length];

            offset = 0;

            length = 0;

            isInText = false;

            for (; offset < argText.Length; ++offset)
            {
                var chr = argText[offset];

                if (!isInText && char.IsWhiteSpace(chr))
                {
                    break;
                }

                if (chr is '"' && !isInText)
                {
                    isInText = true;

                    continue;
                }

                if (chr is '"' or '\\' && offset + 1 < argText.Length && argText[offset + 1] is '"')
                {
                    chars[length] = '"';
                    ++length;
                    ++offset;

                    continue;
                }

                if (chr is '"')
                {
                    isInText = false;

                    continue;
                }

                chars[length] = chr;
                ++length;
            }

            Debug.Assert(length == chars.Length);

            return new string(chars);
        }

        /// <summary>
        /// 一个参数转换为 Chrome 命令行的展现形式。
        /// </summary>
        /// <param name="argText"></param>
        /// <returns></returns>
        public static string ToArg(ReadOnlySpan<char> argText)
        {
            if (argText.IndexOfAny("\b\f\n\r\v".AsSpan()) >= 0)
            {
                throw new NotSupportedException();
            }

            if (argText.IndexOf('"') >= 0)
            {
                return $"\"{argText.ToString().Replace("\"", "\"\"")}\"";
            }

            return argText.ToString();
        }

        /// <summary>
        /// 将 Chrome 进程设置为无头模式。
        /// </summary>
        public static void SetToHeadlessMode(this ChromeProcess chromeProcess)
        {
            if (chromeProcess.Running)
            {
                throw new InvalidOperationException("Cannot set already running process.");
            }

            chromeProcess.StartInfo.Arguments =
                "--headless " +
                "--disable-gpu " +
                "--hide-scrollbars " +
                "--mute-audio " +
                "--disable-background-networking " +
                "--disable-background-timer-throttling " +
                "--disable-default-apps " +
                "--disable-extensions " +
                "--disable-hang-monitor " +
                "--disable-prompt-on-repost " +
                "--disable-sync " +
                "--disable-translate " +
                "--metrics-recording-only " +
                "--no-first-run " +
                "--disable-crash-reporter " +
                "--remote-debugging-port=0 ";
        }

        /// <summary>
        /// 为 Chrome 进程设置用户数据目录。
        /// </summary>
        public static void SetUserDataDir(this ChromeProcess chromeProcess, DirectoryInfo userDataDir)
        {
            chromeProcess.SetArg(UserDataDirArgName, userDataDir.FullName);
        }

        /// <summary>
        /// 为 Chrome 进程设置远程调试端口。端口为 0 则 Chrome 会自动获取可用端口。
        /// </summary>
        public static void SetRemoteDebugginPort(this ChromeProcess chromeProcess, int remoteDebuggingPort = 0)
        {
            chromeProcess.SetArg(RemoteDebuggingPortName, remoteDebuggingPort.ToString());
        }

        internal static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, CancellationToken cancellationToken)
        {
            return await task.ContinueWith(x => x.Result, cancellationToken); ;
        }
    }

    sealed record BrowserVersionInfo(
        [property: JsonPropertyName("Browser")]
        string Browser,
        [property: JsonPropertyName("Protocol-Version")]
        string ProtocolVersion,
        [property: JsonPropertyName("User-Agent")]
        string UserAgent,
        [property: JsonPropertyName("V8-Version")]
        string V8Version,
        [property: JsonPropertyName("WebKit-Version")]
        string WebKitVersion,
        [property: JsonPropertyName("webSocketDebuggerUrl")]
        string WebSocketDebuggerUrl
        );
}