namespace PushSharp.Net.Models;

/// <summary>
/// Provider-agnostic push notification payload.
/// All properties are optional except that at least Title or Body should be set for visible notifications.
/// </summary>
public sealed class PushNotification
{
    /// <summary>Notification title. Displayed in the notification header.</summary>
    public string? Title { get; init; }

    /// <summary>Notification body text.</summary>
    public string? Body { get; init; }

    /// <summary>Custom key-value data delivered to the app. Neither FCM nor APNs display this.</summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }

    /// <summary>Sound to play. Use "default" for the system default sound.</summary>
    public string? Sound { get; init; }

    /// <summary>Badge count to display on the app icon (iOS/macOS). Null = no change.</summary>
    public int? Badge { get; init; }

    /// <summary>URL of an image to display in the notification (FCM large icon / APNs attachment).</summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Collapse key (FCM) / apns-collapse-id (APNs). Notifications with the same key replace each other on device.
    /// </summary>
    public string? CollapseKey { get; init; }

    /// <summary>Expiration time after which the platform should not deliver the notification.</summary>
    public DateTimeOffset? Expiration { get; init; }

    /// <summary>Secondary line below the title. APNs-specific; FCM ignores this field.</summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// When true, sends a background/silent push (content-available).
    /// APNs uses apns-push-type: background + apns-priority: 5; FCM sets content_available.
    /// Default is false (visible alert push).
    /// </summary>
    public bool ContentAvailable { get; init; }
}
