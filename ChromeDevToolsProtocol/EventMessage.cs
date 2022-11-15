namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 事件消息模型。
    /// </summary>
    /// <typeparam name="TParams">事件参数类型</typeparam>
    /// <param name="Method">事件名</param>
    /// <param name="Params">事件参数</param>
    public record EventMessage<TParams>(string Method, TParams Params);
}