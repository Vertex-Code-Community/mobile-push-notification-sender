using Microsoft.Extensions.Logging;
using PushSharp.Net.Models;

namespace PushSharp.Net.Internal;

/// <summary>
/// Internal retry handler with exponential backoff and jitter.
/// Retries transient failures up to 3 times; permanent errors and dead tokens are returned immediately.
/// </summary>
internal static class RetryHandler
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private const double JitterFactor = 0.2;

    private static readonly HashSet<string> PermanentFcmCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNREGISTERED", "INVALID_ARGUMENT", "SENDER_ID_MISMATCH", "THIRD_PARTY_AUTH_ERROR"
    };

    private static readonly HashSet<string> PermanentApnsCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BadDeviceToken", "Unregistered", "DeviceTokenNotForTopic",
        "Forbidden", "InvalidProviderToken", "MissingTopic",
        "TopicDisallowed", "BadCertificate", "BadCertificateEnvironment", "ExpiredProviderToken"
    };

    /// <summary>
    /// Executes a send operation with retry for transient failures.
    /// </summary>
    public static async Task<PushResult> ExecuteAsync(
        Func<Task<PushResult>> operation,
        ILogger logger,
        string tokenForLog,
        CancellationToken cancellationToken)
    {
        PushResult result = null!;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            result = await operation().ConfigureAwait(false);

            if (result.IsSuccess)
                return result;

            if (!ShouldRetry(result))
                return result;

            if (attempt == MaxRetries)
                break;

            var delay = CalculateDelay(attempt, result.RetryAfter);

            logger.LogWarning(
                "Retrying send for token {Token}, attempt {Attempt}/{MaxRetries}, delay {DelayMs}ms, errorCode={ErrorCode}",
                tokenForLog, attempt + 1, MaxRetries, (int)delay.TotalMilliseconds, result.ErrorCode);

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static bool ShouldRetry(PushResult result)
    {
        if (result.IsDeadToken)
            return false;

        if (result.ErrorCode is null)
            return false;

        if (PermanentFcmCodes.Contains(result.ErrorCode))
            return false;

        if (PermanentApnsCodes.Contains(result.ErrorCode))
            return false;

        return true;
    }

    /// <summary>
    /// delay = RetryAfter ?? baseDelay * 2^attempt * (1 ± 0.2 jitter)
    /// </summary>
    private static TimeSpan CalculateDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } ra && ra > TimeSpan.Zero)
            return ra;

        var baseMs = BaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var jitter = baseMs * JitterFactor * (Random.Shared.NextDouble() * 2 - 1);
        var delayMs = Math.Max(0, baseMs + jitter);
        return TimeSpan.FromMilliseconds(delayMs);
    }
}
