using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// OCPI.Net enums use the full English name as the C# member (e.g. <c>CountryCode.India</c>)
    /// and carry the wire-format value (e.g. "IN") in an <see cref="EnumMemberAttribute"/>.
    /// Calling plain <c>.ToString()</c> on one of these enums therefore returns "India", not "IN" —
    /// which both corrupts data and overflows narrow DB columns (CountryCode is 2 chars, Currency
    /// is 3). Use <c>BitzArt.EnumToMemberValue</c>'s <c>.ToMemberValue()</c> extension to go
    /// enum → wire string, and <see cref="ParseMemberValue{T}"/> below to go wire string → enum
    /// (the reverse direction isn't provided by that package).
    /// </summary>
    public static class OcpiEnumMemberHelper
    {
        public static T? ParseMemberValue<T>(string? value) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return null;

            foreach (var enumValue in Enum.GetValues<T>())
            {
                var memberInfo = typeof(T).GetMember(enumValue.ToString());
                var attr = memberInfo.Length > 0 ? memberInfo[0].GetCustomAttribute<EnumMemberAttribute>() : null;
                var memberValue = attr?.Value ?? enumValue.ToString();

                if (string.Equals(memberValue, value, StringComparison.OrdinalIgnoreCase))
                    return enumValue;
            }

            return null;
        }
    }

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
