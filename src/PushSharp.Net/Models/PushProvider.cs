namespace PushSharp.Net.Models;

/// <summary>Push notification provider backend.</summary>
public enum PushProvider
{
    /// <summary>Firebase Cloud Messaging (Android and cross-platform).</summary>
    Fcm,

    /// <summary>Apple Push Notification service (iOS, macOS, watchOS, tvOS).</summary>
    Apns
}
