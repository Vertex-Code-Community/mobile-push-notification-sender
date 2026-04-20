using PushSharp.Net.Abstractions;

namespace PushSharp.Net.Models;

/// <summary>
/// Result of a single push notification send attempt.
/// <see cref="IsDeadToken"/> is first-class — do not rely on <see cref="ErrorCode"/> to detect dead tokens.
/// </summary>
public sealed class PushResult
{
    /// <summary>True when the provider accepted the notification for delivery.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// True when the provider indicates the device token is no longer valid.
    /// Callers should remove this token from their database.
    /// </summary>
    public bool IsDeadToken { get; init; }

    /// <summary>Provider-specific error code string (e.g., "UNREGISTERED", "BadDeviceToken").</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error description.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Parsed from the provider's Retry-After HTTP header.
    /// Set this in custom <see cref="IPushProvider"/> implementations to enable
    /// the built-in retry handler to honor server-requested backoff.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }
}
