using System.ComponentModel.DataAnnotations;

namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// Configuration for the Firebase Cloud Messaging (FCM HTTP v1) provider.
/// Set via <c>services.AddPushNotifications(push => push.AddFcm(fcm => { ... }))</c>.
/// </summary>
public sealed class FcmOptions
{
    /// <summary>
    /// FCM project ID from the Firebase console (e.g., "my-project-12345").
    /// Used in the send URL: <c>https://fcm.googleapis.com/v1/projects/{ProjectId}/messages:send</c>.
    /// </summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Path to the service account JSON credentials file on disk.
    /// Mutually exclusive with <see cref="CredentialJson"/>.
    /// </summary>
    public string? CredentialFilePath { get; set; }

    /// <summary>
    /// Inline service account JSON content.
    /// Mutually exclusive with <see cref="CredentialFilePath"/>.
    /// </summary>
    public string? CredentialJson { get; set; }
}
