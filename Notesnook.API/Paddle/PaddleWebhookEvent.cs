using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notesnook.API.Paddle
{
    public class PaddleWebhookEvent
    {
        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        [JsonPropertyName("occurred_at")]
        public DateTimeOffset OccurredAt { get; set; }

        [JsonPropertyName("notification_id")]
        public string? NotificationId { get; set; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }
}
