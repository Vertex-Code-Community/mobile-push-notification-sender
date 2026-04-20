using System.ComponentModel.DataAnnotations;

namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// Configuration for the Apple Push Notification service (APNs HTTP/2 + JWT) provider.
/// Set via <c>services.AddPushNotifications(push => push.AddApns(apns => { ... }))</c>.
/// </summary>
public sealed class ApnsOptions
{
    /// <summary>
    /// 10-character key ID from the Apple Developer portal (from the .p8 auth key).
    /// Embedded in the APNs JWT header as <c>kid</c>.
    /// </summary>
    [Required]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// 10-character Apple Developer Team ID.
    /// Embedded in the APNs JWT payload as <c>iss</c>.
    /// </summary>
    [Required]
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// App bundle ID (e.g., "com.example.MyApp").
    /// Sent as the <c>apns-topic</c> header on every APNs request.
    /// </summary>
    [Required]
    public string BundleId { get; set; } = string.Empty;

    /// <summary>
    /// Path to the .p8 private key file on disk.
    /// Mutually exclusive with <see cref="PrivateKeyContent"/>.
    /// </summary>
    public string? PrivateKeyFilePath { get; set; }

    /// <summary>
    /// Inline .p8 private key content (PEM format, including BEGIN/END PRIVATE KEY header/footer).
    /// Mutually exclusive with <see cref="PrivateKeyFilePath"/>.
    /// </summary>
    public string? PrivateKeyContent { get; set; }

    /// <summary>
    /// When true, sends to the APNs sandbox endpoint (<c>api.sandbox.push.apple.com</c>).
    /// Default is false (production endpoint).
    /// </summary>
    public bool UseSandbox { get; set; } = false;
}
