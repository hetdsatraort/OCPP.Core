using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCPI.Core.Roaming.Services
{

    public class SourceGenEnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (value == null) return default;

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = field.GetCustomAttribute<EnumMemberAttribute>();
                if ((attr != null && attr.Value == value) || field.Name == value)
                {
                    return (T)field.GetValue(null)!;
                }
            }
            throw new JsonException($"Unable to parse enum value: {value}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            string name = value.ToString();
            var field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Static);

            if (field != null)
            {
                var attr = field.GetCustomAttribute<EnumMemberAttribute>();
                if (attr != null)
                {
                    writer.WriteStringValue(attr.Value);
                    return;
                }
            }
            writer.WriteStringValue(name);
        }
    }
}
