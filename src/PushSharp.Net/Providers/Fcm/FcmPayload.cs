using System.Text.Json.Serialization;

namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// FCM HTTP v1 request body: <c>{ "message": { ... } }</c>.
/// Serialized with System.Text.Json using WhenWritingNull to omit unset fields.
/// </summary>
internal sealed class FcmRequest
{
    [JsonPropertyName("message")]
    public FcmMessage Message { get; set; } = null!;
}

internal sealed class FcmMessage
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("notification")]
    public FcmNotification? Notification { get; set; }

    [JsonPropertyName("android")]
    public FcmAndroidConfig? Android { get; set; }

    [JsonPropertyName("data")]
    public IDictionary<string, string>? Data { get; set; }
}

internal sealed class FcmNotification
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

internal sealed class FcmAndroidConfig
{
    [JsonPropertyName("collapse_key")]
    public string? CollapseKey { get; set; }

    [JsonPropertyName("ttl")]
    public string? Ttl { get; set; }

    [JsonPropertyName("notification")]
    public FcmAndroidNotification? Notification { get; set; }
}

internal sealed class FcmAndroidNotification
{
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("sound")]
    public string? Sound { get; set; }

    [JsonPropertyName("notification_count")]
    public int? NotificationCount { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

/// <summary>
/// FCM HTTP v1 error response structure.
/// </summary>
internal sealed class FcmErrorResponse
{
    [JsonPropertyName("error")]
    public FcmError? Error { get; set; }
}

internal sealed class FcmError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("details")]
    public List<FcmErrorDetail>? Details { get; set; }
}

internal sealed class FcmErrorDetail
{
    [JsonPropertyName("@type")]
    public string? Type { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
}
