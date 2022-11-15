using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeDevToolsProtocol
{
    internal sealed class EnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public static readonly Dictionary<T, string> Map;
        public static readonly Dictionary<string, T> ReversedMap;

        static EnumConverter()
        {
            Map = new Dictionary<T, string>();

            foreach (var enumFieldInfo in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var value = (T)enumFieldInfo.GetValue(null)!;
                var enumValue = enumFieldInfo.GetCustomAttribute<EnumValueAttribute>()?.Value;

                enumValue ??= enumFieldInfo.Name;

                Map.Add(value, enumValue);
            }

            ReversedMap = Map.ToDictionary(x => x.Value, x => x.Key);
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var enumValue = reader.GetString();

            if (string.IsNullOrEmpty(enumValue))
            {
                return default;
            }

            return ReversedMap[enumValue];
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Map[value]);
        }
    }
}