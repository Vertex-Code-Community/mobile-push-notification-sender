using System.Text.Json.Serialization;

namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// APNs JSON payload: <c>{ "aps": { ... }, "custom_key": "value" }</c>.
/// </summary>
internal sealed class ApnsRequest
{
    [JsonPropertyName("aps")]
    public ApnsAps Aps { get; set; } = new();

    [JsonExtensionData]
    public IDictionary<string, object>? CustomData { get; set; }
}

internal sealed class ApnsAps
{
    [JsonPropertyName("alert")]
    public ApnsAlert? Alert { get; set; }

    [JsonPropertyName("badge")]
    public int? Badge { get; set; }

    [JsonPropertyName("sound")]
    public string? Sound { get; set; }

    [JsonPropertyName("content-available")]
    public int? ContentAvailable { get; set; }

    [JsonPropertyName("mutable-content")]
    public int? MutableContent { get; set; }

    [JsonPropertyName("thread-id")]
    public string? ThreadId { get; set; }
}

internal sealed class ApnsAlert
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

/// <summary>
/// APNs error response body: <c>{ "reason": "BadDeviceToken" }</c>.
/// </summary>
internal sealed class ApnsErrorResponse
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }
}
