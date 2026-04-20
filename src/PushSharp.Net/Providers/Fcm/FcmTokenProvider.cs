using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// Singleton OAuth2 token provider backed by <see cref="GoogleCredential"/>.
/// The credential is constructed once from file or JSON; <c>GetAccessTokenForRequestAsync</c>
/// handles internal caching and refresh (~5 min before expiry).
/// </summary>
internal sealed class FcmTokenProvider : IFcmTokenProvider
{
    private static readonly string[] FcmScopes = ["https://www.googleapis.com/auth/firebase.messaging"];

    private readonly GoogleCredential _credential;
    private readonly ILogger<FcmTokenProvider> _logger;

    public FcmTokenProvider(IOptions<FcmOptions> options, ILogger<FcmTokenProvider> logger)
    {
        _logger = logger;
        var opts = options.Value;

        #pragma warning disable CS0618 // CredentialFactory.FromFile<GoogleCredential> is incompatible with service account JSON
        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(opts.CredentialFilePath))
        {
            credential = GoogleCredential.FromFile(opts.CredentialFilePath);
            _logger.LogDebug("FCM credential loaded from file: {Path}", opts.CredentialFilePath);
        }
        else
        {
            credential = GoogleCredential.FromJson(opts.CredentialJson!);
            _logger.LogDebug("FCM credential loaded from inline JSON");
        }
        #pragma warning restore CS0618

        _credential = credential.CreateScoped(FcmScopes);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return token;
    }
}
