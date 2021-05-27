using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Slp.API.Models
{
    public class ErrorDetails 
    {
        [JsonPropertyName("detail")]
        public string Detail { get; set; }
        [JsonPropertyName("instance")]
        public string Instance { get; set; }
        [JsonPropertyName("status")]
        public int? Status { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("errors")]
        public List<string> Errors { get; } = new List<string>();
    }
}
