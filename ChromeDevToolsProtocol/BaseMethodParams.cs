namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 提供基础消息参数的接口。继承此基类可自动识别泛型参数，自定义消息可不继承此接口。
    /// </summary>
    /// <typeparam name="TParams">参数类型</typeparam>
    /// <typeparam name="TResult">返回值类型</typeparam>
    public interface IMethodParams<TParams, TResult>
        where TParams : IMethodParams<TParams, TResult>
    {
        /// <summary>
        /// 获取此参数所应用的方法名。
        /// </summary>
        public abstract string GetMethod();
    }
}