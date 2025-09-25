using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class UpdateStatusRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("viaQr")]
        public bool ViaQr { get; set; } = false;
    }

    public class DeleteCollectedRequest
    {
        [JsonPropertyName("ids")]
        public List<string> Ids { get; set; } = new();
    }
}