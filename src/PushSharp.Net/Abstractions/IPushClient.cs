using PushSharp.Net.Models;
using PushSharp.Net.Registration;

namespace PushSharp.Net.Abstractions;

/// <summary>
/// Primary entry point for sending push notifications.
/// Resolve via DI after calling <c>services.AddPushNotifications()</c>.
/// </summary>
public interface IPushClient
{
    /// <summary>
    /// Sends a push notification to a single device token.
    /// Token format determines provider routing automatically.
    /// </summary>
    Task<PushResult> SendAsync(string deviceToken, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the same notification to multiple device tokens.
    /// One token failure does not abort the rest.
    /// </summary>
    Task<BatchPushResult> SendBatchAsync(IEnumerable<string> deviceTokens, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all devices registered to a specific user.
    /// Requires an <see cref="IDeviceRegistrationRepository"/> to be registered via
    /// <c>builder.AddDeviceStore&lt;T&gt;()</c>.
    /// Dead tokens are automatically cleaned up from the store.
    /// </summary>
    Task<BatchPushResult> SendToUserAsync(string userId, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all devices that have a specific tag.
    /// Requires an <see cref="IDeviceRegistrationRepository"/> to be registered via
    /// <c>builder.AddDeviceStore&lt;T&gt;()</c>.
    /// Dead tokens are automatically cleaned up from the store.
    /// </summary>
    Task<BatchPushResult> SendByTagAsync(string tag, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all registered devices.
    /// Requires an <see cref="IDeviceRegistrationRepository"/> to be registered.
    /// Use with caution for large audiences.
    /// </summary>
    Task<BatchPushResult> SendToAllAsync(PushNotification notification, CancellationToken cancellationToken = default);
}
