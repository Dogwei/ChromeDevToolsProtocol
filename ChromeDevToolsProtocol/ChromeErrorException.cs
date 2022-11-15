namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// Chrome 错误异常。
    /// </summary>
    public class ChromeErrorException : Exception
    {
        /// <summary>
        /// Chrome 错误信息。
        /// </summary>
        public ChromeErrorInfo Error { get; }

        /// <summary>
        /// 初始化Chrome 错误异常。
        /// </summary>
        /// <param name="error">Chrome 错误信息</param>
        public ChromeErrorException(ChromeErrorInfo error) : base(error.Message)
        {
            Error = error;
        }
    }
}