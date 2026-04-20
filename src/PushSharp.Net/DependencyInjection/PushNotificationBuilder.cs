using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Providers.Apns;
using PushSharp.Net.Providers.Fcm;
using PushSharp.Net.Registration;

namespace PushSharp.Net.DependencyInjection;

/// <summary>
/// Fluent builder for configuring push notification providers.
/// Obtained via <see cref="ServiceCollectionExtensions.AddPushNotifications(IServiceCollection, Action{PushNotificationBuilder})"/>.
/// </summary>
public sealed class PushNotificationBuilder
{
    internal IServiceCollection Services { get; }

    internal PushNotificationBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Adds the Firebase Cloud Messaging (FCM HTTP v1) provider.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "FcmOptions is sealed with known properties; DataAnnotation validators are safe.")]
    public PushNotificationBuilder AddFcm(Action<FcmOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Services.AddOptions<FcmOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        Services.AddSingleton<IValidateOptions<FcmOptions>, FcmOptionsValidator>();

        Services.AddHttpClient("PushSharp.Fcm")
            .UseSocketsHttpHandler((handler, _) =>
            {
                handler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        Services.AddSingleton<IFcmTokenProvider, FcmTokenProvider>();
        Services.AddSingleton<IPushProvider, FcmProvider>();

        return this;
    }

    /// <summary>
    /// Adds the Apple Push Notification service (APNs HTTP/2 + JWT) provider.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "ApnsOptions is sealed with known properties; DataAnnotation validators are safe.")]
    public PushNotificationBuilder AddApns(Action<ApnsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Services.AddOptions<ApnsOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        Services.AddSingleton<IValidateOptions<ApnsOptions>, ApnsOptionsValidator>();

        Services.AddHttpClient("PushSharp.Apns")
            .UseSocketsHttpHandler((handler, _) =>
            {
                handler.EnableMultipleHttp2Connections = true;
                handler.PooledConnectionLifetime = TimeSpan.FromMinutes(30);
                handler.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
                handler.KeepAlivePingTimeout = TimeSpan.FromSeconds(10);
                handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests;
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        Services.AddSingleton<IApnsJwtProvider, ApnsJwtProvider>();
        Services.AddSingleton<IPushProvider, ApnsProvider>();

        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPushProvider"/> implementation as a singleton.
    /// The provider's <see cref="IPushProvider.CanHandle"/> determines which tokens it receives.
    /// </summary>
    public PushNotificationBuilder AddProvider<TProvider>() where TProvider : class, IPushProvider
    {
        Services.AddSingleton<IPushProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPushProvider"/> instance directly.
    /// Useful for providers that require constructor parameters not available in DI.
    /// </summary>
    public PushNotificationBuilder AddProvider(IPushProvider instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Registers a device registration store implementation.
    /// Required for <see cref="IPushClient.SendToUserAsync"/>,
    /// <see cref="IPushClient.SendByTagAsync"/>, and <see cref="IPushClient.SendToAllAsync"/>.
    /// <para>
    /// The consuming application implements <see cref="IDeviceRegistrationRepository"/>
    /// using its own database and registers it here.
    /// </para>
    /// </summary>
    public PushNotificationBuilder AddDeviceStore<TStore>() where TStore : class, IDeviceRegistrationRepository
    {
        Services.AddSingleton<IDeviceRegistrationRepository, TStore>();
        return this;
    }

    /// <summary>
    /// Registers a device registration store instance directly.
    /// </summary>
    public PushNotificationBuilder AddDeviceStore(IDeviceRegistrationRepository instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Services.AddSingleton(instance);
        return this;
    }
}
