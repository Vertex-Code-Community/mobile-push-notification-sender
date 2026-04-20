namespace PushSharp.Net.Registration;

/// <summary>
/// Represents a registered device that can receive push notifications.
/// This is the model that <see cref="IDeviceRegistrationRepository"/> persists.
/// </summary>
public sealed class DeviceRegistration
{
    /// <summary>Unique device/installation identifier (e.g., Android ID, IDFV).</summary>
    public required string DeviceId { get; init; }

    /// <summary>The push token issued by FCM or APNs.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Platform hint: "fcm", "apns", or a custom provider key.</summary>
    public required string Platform { get; init; }

    /// <summary>Application user ID that owns this device. Null for anonymous devices.</summary>
    public string? UserId { get; init; }

    /// <summary>Tags for group targeting (e.g., "premium", "region:eu").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
