using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// ES256 JWT provider for APNs authentication.
/// Loads the .p8 private key once, caches signed JWTs for 55 minutes,
/// and regenerates with a <see cref="SemaphoreSlim"/> double-check latch.
/// </summary>
internal sealed class ApnsJwtProvider : IApnsJwtProvider, IDisposable
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(55);

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _teamId;
    private readonly ILogger<ApnsJwtProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public ApnsJwtProvider(IOptions<ApnsOptions> options, ILogger<ApnsJwtProvider> logger)
    {
        _logger = logger;
        var opts = options.Value;
        _keyId = opts.KeyId;
        _teamId = opts.TeamId;

        string pemContent;
        if (!string.IsNullOrWhiteSpace(opts.PrivateKeyFilePath))
        {
            pemContent = File.ReadAllText(opts.PrivateKeyFilePath);
            _logger.LogDebug("APNs private key loaded from file: {Path}", opts.PrivateKeyFilePath);
        }
        else
        {
            pemContent = opts.PrivateKeyContent!;
            _logger.LogDebug("APNs private key loaded from inline content");
        }

        _key = LoadECDsaKey(pemContent);
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            _cachedToken = GenerateJwt();
            _tokenExpiry = DateTimeOffset.UtcNow.Add(CacheLifetime);
            _logger.LogDebug("APNs JWT regenerated, expires at {Expiry}", _tokenExpiry);

            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private string GenerateJwt()
    {
        // Header: {"alg":"ES256","kid":"<keyId>"}
        var header = $"{{\"alg\":\"ES256\",\"kid\":\"{_keyId}\"}}";

        // Payload: {"iss":"<teamId>","iat":<unixSeconds>}
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{{\"iss\":\"{_teamId}\",\"iat\":{iat}}}";

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signingInput = $"{headerBase64}.{payloadBase64}";

        var signature = _key.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256);

        var signatureBase64 = Base64UrlEncode(signature);
        return $"{signingInput}.{signatureBase64}";
    }

    private static ECDsa LoadECDsaKey(string pem)
    {
        // Strip PEM header/footer and whitespace, then base64-decode DER bytes
        var base64 = pem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        var keyBytes = Convert.FromBase64String(base64);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);
        return ecdsa;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        _key.Dispose();
    }
}
