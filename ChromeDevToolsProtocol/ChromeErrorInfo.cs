namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// Chrome 错误信息。
    /// </summary>
    /// <param name="Code">错误代码</param>
    /// <param name="Message">错误消息</param>
    public record ChromeErrorInfo(int Code, string Message);
}