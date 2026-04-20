namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// Provides OAuth2 access tokens for FCM HTTP v1 API calls.
/// Registered as a singleton; the underlying <c>GoogleCredential</c> handles token refresh internally.
/// </summary>
internal interface IFcmTokenProvider
{
    /// <summary>
    /// Returns a valid OAuth2 Bearer token for the FCM API.
    /// The token is cached internally and refreshed ~5 minutes before expiry.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
