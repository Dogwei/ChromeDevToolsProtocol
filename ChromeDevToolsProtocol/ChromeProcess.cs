using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 表示 Chrome 进程。
    /// </summary>
    public class ChromeProcess : Process
    {
        /// <summary>
        /// 监听地址匹配正则，通常无需修改。
        /// </summary>
        public static Regex ListeningUriRegex { get; set; } = new Regex(@"^DevTools listening on (?<Uri>.*)$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// 日志正则，通常无需修改。
        /// </summary>
        public static Regex LogRegex { get; set; } = new Regex(@"^\[(?<Time>[A-Za-z0-9\-\\/+\. ]+):(?<LogLevel>[A-Za-z0-9_]+):(?<Localtion>[A-Za-z0-9_\-\.$]+)(\((?<RowNumber>[0-9]+)\))?\](?<Message>.*)$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Chrome 可执行文件完整名称。
        /// </summary>
        public static string? ChromeExeFileName { get; set; } = ChromeFinder.Find();

        private static string? MatchListeningUri(string line)
        {
            var match = ListeningUriRegex.Match(line);

            if (!match.Success)
            {
                return null;
            }

            return match.Groups["Uri"].Value;
        }

        private static ChromeLogInfo? MatchLog(string line)
        {
            var match = LogRegex.Match(line);

            if (!match.Success)
            {
                return null;
            }

            return new ChromeLogInfo(
                match.Groups["Time"].Value,
                match.Groups["LogLevel"].Value,
                match.Groups["Localtion"].Value,
                match.Groups["RowNumber"].Value,
                match.Groups["Message"].Value,
                line
                );
        }


        readonly CancellationTokenSource cancellationTokenSource;
        readonly TaskCompletionSource<string> listeningUriTaskTaskCompletionSource;

        bool running;

        /// <summary>
        /// 初始化 Chrome 进程。
        /// </summary>
        public ChromeProcess()
        {
            StartInfo = new ProcessStartInfo
            {
                Arguments = "",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            if (ChromeExeFileName != null)
            {
                StartInfo.FileName = ChromeExeFileName;
            }

            cancellationTokenSource = new CancellationTokenSource();
            listeningUriTaskTaskCompletionSource = new TaskCompletionSource<string>();
        }

        /// <summary>
        /// 表示 Chrome 进程是否正在运行。
        /// </summary>
        public bool Running => running;

        /// <summary>
        /// 当出现未处理的异常时触发。
        /// </summary>
        public event EventHandler<Exception>? UnhandledException;

        /// <summary>
        /// 当在标准输出流中读取到一行时触发。
        /// </summary>
        public event EventHandler<string>? LineReceived;

        /// <summary>
        /// 当在标准输出流中读取到日志时触发。
        /// </summary>
        public event EventHandler<ChromeLogInfo>? LogReceived;

        private async ValueTask ReadLines(StreamReader streamReader, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await streamReader.ReadLineAsync().WaitAsync(cancellationToken);

                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!listeningUriTaskTaskCompletionSource.Task.IsCompleted && MatchListeningUri(line) is string listeningUri)
                    {
                        listeningUriTaskTaskCompletionSource.TrySetResult(listeningUri);
                    }

                    if (LogReceived is EventHandler<ChromeLogInfo> logReceived && MatchLog(line) is ChromeLogInfo chromeLog)
                    {
                        logReceived(this, chromeLog);
                    }

                    if (LineReceived is EventHandler<string> lineReceived)
                    {
                        lineReceived(this, line);
                    }
                }
            }
            catch (Exception e)
            {
                UnhandledException?.Invoke(this, e);
            }
            finally
            {
                running = false;
            }
        }

        /// <summary>
        /// 启动 Chrome 进程，并开始接收标准输出流中内容。
        /// </summary>
        /// <returns>返回是否启动成功</returns>
        public new bool Start()
        {
            EnableRaisingEvents = true;

            var result = base.Start();

            if (result)
            {
                running = true;

                Task.Run(() => ReadLines(StandardError, cancellationTokenSource.Token));
            }

            return result;
        }

        /// <summary>
        /// 是否资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            cancellationTokenSource.Cancel();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 在标准输出流中获取远程调试监听路径。仅支持无头模式。
        /// </summary>
        /// <param name="throwExceptionWhenErrorLog">是否在接收到错误日志时引发异常</param>
        /// <param name="cancellationToken">可设置取消标志</param>
        /// <returns>返回远程调试监听路径</returns>
        /// <exception cref="NotSupportedException">仅支持无头模式。</exception>
        /// <exception cref="InvalidOperationException">进程未启动</exception>
        public async ValueTask<string> GetListeningUriFromStandardErrorAsync(bool throwExceptionWhenErrorLog = true, CancellationToken cancellationToken = default)
        {
            if (!HasArg(Helper.HeadlessArgName))
            {
                throw new NotSupportedException("Only supports process is --headless mode.");
            }

            var remoteDebuggingPort = GetArgValue(Helper.RemoteDebuggingPortName);

            if (string.IsNullOrEmpty(remoteDebuggingPort) || !int.TryParse(remoteDebuggingPort, out var intRemoteDebuggingPort) || intRemoteDebuggingPort != 0)
            {
                throw new NotSupportedException("The --remote-debugging-port argument must be specified as 0 to be available.");
            }

            if (!running)
            {
                throw new InvalidOperationException();
            }

            if (throwExceptionWhenErrorLog)
            {
                var errorLogTaskCompletionSource = new TaskCompletionSource<string>();

                LogReceived += ChromeProcess_LogReceived;

                try
                {
                    return await await Task.WhenAny(
                        listeningUriTaskTaskCompletionSource.Task,
                        errorLogTaskCompletionSource.Task.WaitAsync(cancellationToken)
                        );
                }
                finally
                {
                    LogReceived -= ChromeProcess_LogReceived;
                }

                void ChromeProcess_LogReceived(object? sender, ChromeLogInfo e)
                {
                    if (e.IsErrorLog())
                    {
                        errorLogTaskCompletionSource.TrySetException(new ChromeErrorLogException(e));
                    }
                }
            }
            else
            {
                return await listeningUriTaskTaskCompletionSource.Task.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 设置进程启动参数。
        /// </summary>
        /// <param name="name">参数名</param>
        /// <exception cref="InvalidOperationException">进程已在运行中</exception>
        public void SetArg(string name)
        {
            if (Running)
            {
                throw new InvalidOperationException("Cannot set already running process.");
            }

            StartInfo.Arguments = $"{StartInfo.Arguments.TrimEnd()} {name}";
        }

        /// <summary>
        /// 设置进程启动参数。
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        /// <exception cref="InvalidOperationException">进程已在运行中</exception>
        public void SetArg(string name, string value)
        {
            if (Running)
            {
                throw new InvalidOperationException("Cannot set already running process.");
            }

            StartInfo.Arguments = $"{StartInfo.Arguments.TrimEnd()} {name}={Helper.ToArg(value.AsSpan())}";
        }

        /// <summary>
        /// 获取进程启动参数值。
        /// </summary>
        /// <param name="name">参数名</param>
        /// <returns>返回进程启动参数值</returns>
        public string? GetArgValue(string name)
        {
            var arguments = StartInfo.Arguments;

            var index = GetArgIndex(name, out var hasValue);

            if (!hasValue)
            {
                return null;
            }

            return Helper.ParseArg(arguments.AsSpan().Slice(index + name.Length + 1), out _);
        }

        private int GetArgIndex(string name, out bool hasValue)
        {
            hasValue = false;

            var arguments = StartInfo.Arguments;

            int index = -1;

        Next:

            index = arguments.IndexOf(name, index + 1);

            if (index is -1)
            {
                return -1;
            }

            if (index != 0 && !char.IsWhiteSpace(arguments[index - 1]))
            {
                goto Next;
            }

            if (index + name.Length == arguments.Length)
            {
                return index;
            }

            var chr = arguments[index + name.Length];

            if (char.IsWhiteSpace(chr))
            {
                return index;
            }

            if (chr == '=')
            {
                hasValue = true;

                return index;
            }

            goto Next;
        }

        /// <summary>
        /// 判断是否存在启动参数。
        /// </summary>
        /// <param name="name">参数名</param>
        public bool HasArg(string name)
        {
            var index = GetArgIndex(name, out _);

            return index != -1;
        }

        /// <summary>
        /// 移除启动参数。
        /// </summary>
        /// <param name="name">参数名</param>
        /// <returns>返回是否移除了参数</returns>
        /// <exception cref="InvalidOperationException">进程已在运行中</exception>
        public bool RemoveArg(string name)
        {
            if (Running)
            {
                throw new InvalidOperationException("Cannot set already running process.");
            }

            var arguments = StartInfo.Arguments;

            var index = GetArgIndex(name, out var hasValue);

            if (index is -1)
            {
                return false;
            }

            int length = name.Length;

            if (hasValue)
            {
                Helper.ParseArg(arguments.AsSpan().Slice(index + name.Length + 1), out var parsedLength);

                length += 1 + parsedLength;
            }

            if (index != 0 && char.IsWhiteSpace(arguments[index - 1]))
            {
                --index;
                ++length;
            }
            else if (index + length != arguments.Length && char.IsWhiteSpace(arguments[index + length]))
            {
                ++length;
            }

            StartInfo.Arguments = arguments.Remove(index, length);

            return true;
        }
    }
}