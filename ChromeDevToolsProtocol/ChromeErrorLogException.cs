namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// Chrome 错误日志异常。
    /// </summary>
    public class ChromeErrorLogException : Exception
    {
        /// <summary>
        /// Chrome 错误日志信息。
        /// </summary>
        public ChromeLogInfo ChromeLog { get; }

        /// <summary>
        /// 初始化 Chrome 错误日志异常。
        /// </summary>
        /// <param name="chromeLog">Chrome 错误日志信息</param>
        public ChromeErrorLogException(ChromeLogInfo chromeLog) : base(chromeLog.Message)
        {
            ChromeLog = chromeLog;
        }
    }
}