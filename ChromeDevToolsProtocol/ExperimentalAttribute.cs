namespace ChromeDevToolsProtocol
{
    /// <summary>
    /// 实验性的对象标识。
    /// </summary>
    public class ExperimentalAttribute : Attribute
    {

    }

    /// <summary>
    /// 枚举值标识。
    /// </summary>
    public class EnumValueAttribute : Attribute
    {
        /// <summary>
        /// 枚举值
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 初始化枚举值标识。
        /// </summary>
        /// <param name="value">枚举值</param>
        public EnumValueAttribute(string value)
        {
            Value = value;
        }
    }
}