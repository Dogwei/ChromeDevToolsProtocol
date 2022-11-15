using System;
using System.Diagnostics;

namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 域的基类。
    /// </summary>
    public abstract class BaseDomain
    {
        /// <summary>
        /// 初始化域的基类。
        /// </summary>
        /// <param name="client">域实例所属客户端</param>
        protected BaseDomain(BaseClient client)
        {
            Client = client;
        }

        /// <summary>
        /// 域实例所属客户端。
        /// </summary>
        public BaseClient Client { get; }

        /// <summary>
        /// 获取域的名称。
        /// </summary>
        public abstract string DomainName { get; }

        /// <summary>
        /// 触发事件。
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="messageBytes">消息体字节码</param>
        public abstract void RaiseEvent(string eventName, Span<byte> messageBytes);

        /// <summary>
        /// 触发事件。
        /// </summary>
        /// <typeparam name="TEventArgs">事件参数类型</typeparam>
        /// <param name="eventHandler">事件处理器</param>
        /// <param name="messageBytes">消息体字节码</param>
        protected void RaiseEvent<TEventArgs>(EventHandler<TEventArgs>? eventHandler, Span<byte> messageBytes)
        {
            if (eventHandler != null)
            {
                var message = Client.DeserializeMessage<EventMessage<TEventArgs>>(messageBytes);

                Debug.Assert(message != null);

                eventHandler(this, message.Params);
            }
        }

        /// <summary>
        /// 触发未知事件。
        /// </summary>
        /// <param name="eventName">未知事件的名称</param>
        /// <param name="messageBytes">消息体字节码</param>
        public void RaiseUnknownEvent(string eventName, Span<byte> messageBytes)
        {
            Client.RaiseUnknownEvent(DomainName, eventName, messageBytes);
        }
    }
}