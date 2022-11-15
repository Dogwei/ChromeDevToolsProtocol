namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// Chrome 日志信息。
    /// </summary>
    /// <param name="Time">记录时间</param>
    /// <param name="LogLevel">日志等级</param>
    /// <param name="Localtion">引发文件</param>
    /// <param name="RowNumber">引发行数</param>
    /// <param name="Message">日志消息</param>
    /// <param name="Original">日志原文</param>
    public record ChromeLogInfo(string Time, string LogLevel, string Localtion, string RowNumber, string Message, string Original);
}