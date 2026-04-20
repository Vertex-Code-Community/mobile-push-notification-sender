using PushSharp.Net.DependencyInjection;
using PushSharp.Net.Models;

namespace PushSharp.Net.Abstractions;

/// <summary>
/// Extension point for push notification providers.
/// Implement this interface to add support for a new push service (e.g., Huawei, Web Push).
/// Register via <c>builder.AddProvider&lt;T&gt;()</c> in <see cref="ServiceCollectionExtensions.AddPushNotifications"/>.
/// </summary>
public interface IPushProvider
{
    /// <summary>Returns true when this provider handles the given device token format.</summary>
    bool CanHandle(string deviceToken);

    /// <summary>Sends a push notification to a single device token.</summary>
    Task<PushResult> SendAsync(string deviceToken, PushNotification notification, CancellationToken cancellationToken);
}