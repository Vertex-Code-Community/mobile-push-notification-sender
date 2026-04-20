using System.Text.RegularExpressions;
using PushSharp.Net.Models;

namespace PushSharp.Net.Internal;

/// <summary>
/// Classifies device tokens by format to determine which provider should handle them.
/// APNs tokens are exactly 64 lowercase hex characters. All other formats are routed to FCM.
/// </summary>
internal static partial class TokenDetector
{
    /// <summary>
    /// Source-generated compiled regex. Zero-allocation on .NET 7+.
    /// Pattern: exactly 64 hex characters (case-insensitive for defensive matching).
    /// </summary>
    [GeneratedRegex(@"^[0-9a-f]{64}$", RegexOptions.IgnoreCase)]
    private static partial Regex ApnsTokenRegex();

    /// <summary>
    /// Detects the provider for a device token based on its format.
    /// Returns <see cref="PushProvider.Apns"/> for 64 hex chars; <see cref="PushProvider.Fcm"/> for everything else.
    /// </summary>
    public static PushProvider Detect(string token) =>
        ApnsTokenRegex().IsMatch(token) ? PushProvider.Apns : PushProvider.Fcm;
}
