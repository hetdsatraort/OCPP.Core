using System.Text.Json.Serialization;

namespace OCPP.Core.Management.Models
{
    public class AppVersionInfoResponseDto
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("data")]
        public AppVersionInfoDto Data { get; set; }
    }

    public class AppVersionInfoDto
    {
        [JsonPropertyName("latest_version_android")]
        public string latest_version_android { get; set; }

        [JsonPropertyName("latest_version_ios")]
        public string latest_version_ios { get; set; }

        [JsonPropertyName("force_update")]
        public bool force_update { get; set; }

        [JsonPropertyName("message")]
        public string message { get; set; }

        [JsonPropertyName("android_store_url")]
        public string android_store_url { get; set; }

        [JsonPropertyName("ios_store_url")]
        public string ios_store_url { get; set; }
    }
}
