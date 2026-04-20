namespace PushSharp.Net.Models;

/// <summary>Per-token outcome within a <see cref="BatchPushResult"/>.</summary>
public sealed class TokenResult
{
    /// <summary>The device token this result corresponds to.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>The send result for this token.</summary>
    public required PushResult Result { get; init; }
}
