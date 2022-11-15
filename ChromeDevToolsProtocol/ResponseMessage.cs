namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 响应消息模型。
    /// </summary>
    /// <typeparam name="TResult">响应结果类型</typeparam>
    /// <param name="Id">消息 Id</param>
    /// <param name="Result">响应结果</param>
    /// <param name="Error">Chrome 错误信息</param>
    public record ResponseMessage<TResult>(int Id, TResult? Result, ChromeErrorInfo? Error);
}