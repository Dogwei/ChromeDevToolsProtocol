namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 请求消息模型。
    /// </summary>
    /// <typeparam name="TParams">参数类型</typeparam>
    /// <param name="Id">消息 Id</param>
    /// <param name="Method">方法名</param>
    /// <param name="Params">消息参数</param>
    public record RequestMessage<TParams>(int Id, string Method, TParams Params);
}