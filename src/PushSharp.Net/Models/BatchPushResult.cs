using PushSharp.Net.Abstractions;

namespace PushSharp.Net.Models;

/// <summary>
/// Aggregated result of a <see cref="IPushClient.SendBatchAsync"/> call.
/// </summary>
public sealed class BatchPushResult
{
    /// <summary>Number of tokens for which the provider accepted the notification.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of tokens for which the send failed.</summary>
    public int FailureCount { get; init; }

    /// <summary>Per-token outcomes. Count equals the number of tokens passed to SendBatchAsync.</summary>
    public IReadOnlyList<TokenResult> Results { get; init; } = [];
}
