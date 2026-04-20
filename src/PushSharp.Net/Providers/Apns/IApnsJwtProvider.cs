namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// Provides cached ES256 JWTs for APNs authentication.
/// The JWT is regenerated every 55 minutes (Apple's limit is 60 min).
/// </summary>
internal interface IApnsJwtProvider
{
    /// <summary>
    /// Returns a valid APNs JWT Bearer token.
    /// The token is cached and regenerated before the 60-minute Apple expiry.
    /// </summary>
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
