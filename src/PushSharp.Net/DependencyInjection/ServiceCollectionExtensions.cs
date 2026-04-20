using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Internal;
using PushSharp.Net.Providers.Apns;
using PushSharp.Net.Providers.Fcm;

namespace PushSharp.Net.DependencyInjection;

/// <summary>
/// DI registration extensions for PushSharp.Net.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PushSharp.Net with no providers pre-configured (for example when only custom providers are registered later via another path).
    /// Prefer <see cref="AddPushNotifications(IServiceCollection, Action{PushNotificationBuilder})"/> to configure FCM/APNs.
    /// </summary>
    public static IServiceCollection AddPushNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPushClient, PushClient>();
        return services;
    }

    /// <summary>
    /// Registers PushSharp.Net services using the fluent <see cref="PushNotificationBuilder"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddPushNotifications(push =>
    /// {
    ///     push.AddFcm(fcm => { fcm.ProjectId = "my-project"; });
    ///     push.AddApns(apns => { apns.KeyId = "ABC123"; apns.TeamId = "DEF456"; apns.BundleId = "com.example.app"; });
    ///     push.AddProvider&lt;MyCustomProvider&gt;();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPushNotifications(
        this IServiceCollection services,
        Action<PushNotificationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new PushNotificationBuilder(services);
        configure(builder);

        services.AddSingleton<IPushClient, PushClient>();

        return services;
    }

    /// <summary>
    /// Legacy registration using separate FCM and APNs option delegates. Prefer <see cref="AddPushNotifications(IServiceCollection, Action{PushNotificationBuilder})"/>.
    /// </summary>
    [Obsolete("Use AddPushNotifications(push => { push.AddFcm(...); push.AddApns(...); }) instead.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Options types are sealed with known properties; DataAnnotation validators are safe.")]
    public static IServiceCollection AddPushNotificationsLegacy(
        this IServiceCollection services,
        Action<FcmOptions>? configureFcm = null,
        Action<ApnsOptions>? configureApns = null)
    {
        return services.AddPushNotifications(builder =>
        {
            if (configureFcm is not null)
                builder.AddFcm(configureFcm);
            if (configureApns is not null)
                builder.AddApns(configureApns);
        });
    }
}
