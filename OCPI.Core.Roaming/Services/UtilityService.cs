using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCPI.Core.Roaming.Services
{

    public class JsonStringEnumMemberConverterV2 : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(EnumMemberConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }

        private class EnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            private readonly Dictionary<string, T> _stringToEnum = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<T, string> _enumToString = new();

            public EnumMemberConverter()
            {
                foreach (var value in Enum.GetValues<T>())
                {
                    var memInfo = typeof(T).GetMember(value.ToString());
                    var attr = memInfo[0].GetCustomAttribute<EnumMemberAttribute>();

                    string name = attr?.Value ?? value.ToString();

                    _stringToEnum[name] = value;
                    _enumToString[value] = name;
                }
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string stringValue = reader.GetString();
                if (_stringToEnum.TryGetValue(stringValue, out var enumValue))
                {
                    return enumValue;
                }
                throw new JsonException($"Unable to convert \"{stringValue}\" to enum {typeof(T).Name}.");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(_enumToString[value]);
            }
        }
    }
}
