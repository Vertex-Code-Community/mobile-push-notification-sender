using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Models;

namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// APNs HTTP/2 provider implementation.
/// Posts to <c>https://api[.sandbox].push.apple.com/3/device/{token}</c>.
/// Every request uses HTTP/2 exclusively — APNs rejects HTTP/1.1.
/// </summary>
internal sealed class ApnsProvider : IPushProvider
{
    private const string ProductionHost = "https://api.push.apple.com";
    private const string SandboxHost = "https://api.sandbox.push.apple.com";

    private static readonly HashSet<string> DeadTokenReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "BadDeviceToken",
        "Unregistered"
    };

    private static readonly HashSet<string> PermanentFailureReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeviceTokenNotForTopic",
        "Forbidden",
        "InvalidProviderToken",
        "MissingTopic",
        "TopicDisallowed",
        "BadCertificate",
        "BadCertificateEnvironment",
        "ExpiredProviderToken"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApnsJwtProvider _jwtProvider;
    private readonly string _baseUrl;
    private readonly string _bundleId;
    private readonly ILogger<ApnsProvider> _logger;

    public ApnsProvider(
        IHttpClientFactory httpClientFactory,
        IApnsJwtProvider jwtProvider,
        IOptions<ApnsOptions> options,
        ILogger<ApnsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _jwtProvider = jwtProvider;
        _logger = logger;

        var opts = options.Value;
        _bundleId = opts.BundleId;
        _baseUrl = opts.UseSandbox ? SandboxHost : ProductionHost;

        _logger.LogInformation("APNs provider initialized: endpoint={Endpoint}, bundleId={BundleId}",
            _baseUrl, _bundleId);
    }

    public bool CanHandle(string deviceToken) =>
        Internal.TokenDetector.Detect(deviceToken) == PushProvider.Apns;

    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        var payload = BuildPayload(notification);
        var json = JsonSerializer.Serialize(payload, Providers.Apns.ApnsJsonContext.Default.ApnsRequest);

        var httpClient = _httpClientFactory.CreateClient("PushSharp.Apns");
        var jwt = await _jwtProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        var url = $"{_baseUrl}/3/device/{deviceToken}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Version = HttpVersion.Version20;
        httpRequest.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", jwt);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // Required APNs headers
        httpRequest.Headers.TryAddWithoutValidation("apns-topic", _bundleId);

        bool isBackground = notification.ContentAvailable
                            && notification.Title is null
                            && notification.Body is null;

        httpRequest.Headers.TryAddWithoutValidation("apns-push-type", isBackground ? "background" : "alert");
        httpRequest.Headers.TryAddWithoutValidation("apns-priority", isBackground ? "5" : "10");

        if (notification.CollapseKey is not null)
            httpRequest.Headers.TryAddWithoutValidation("apns-collapse-id", notification.CollapseKey);

        if (notification.Expiration is { } expiration)
        {
            var unix = expiration.ToUnixTimeSeconds();
            httpRequest.Headers.TryAddWithoutValidation("apns-expiration", unix.ToString());
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "APNs send failed with exception for token: {Token}", MaskToken(deviceToken));
            return new PushResult
            {
                IsSuccess = false,
                ErrorCode = "TRANSPORT_ERROR",
                ErrorMessage = ex.Message
            };
        }

        if (response.IsSuccessStatusCode)
        {
            return new PushResult { IsSuccess = true };
        }

        return await ClassifyErrorAsync(response, deviceToken, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PushResult> ClassifyErrorAsync(
        HttpResponseMessage response,
        string deviceToken,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? reason = null;

        try
        {
            var errorResponse = JsonSerializer.Deserialize(responseBody, Providers.Apns.ApnsJsonContext.Default.ApnsErrorResponse);
            reason = errorResponse?.Reason;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Could not parse APNs error response for token: {Token}", MaskToken(deviceToken));
        }

        var isDeadToken = reason is not null && DeadTokenReasons.Contains(reason);

        // HTTP 410 is always an unregistered token regardless of reason field
        if (response.StatusCode == HttpStatusCode.Gone)
            isDeadToken = true;

        if (isDeadToken)
        {
            _logger.LogInformation("APNs dead token detected: {Token}, reason: {Reason}",
                MaskToken(deviceToken), reason);
        }
        else if (reason is not null && PermanentFailureReasons.Contains(reason))
        {
            _logger.LogWarning("APNs permanent error for token: {Token}, reason: {Reason}",
                MaskToken(deviceToken), reason);
        }
        else
        {
            _logger.LogWarning(
                "APNs transient error for token: {Token}, status: {StatusCode}, reason: {Reason}",
                MaskToken(deviceToken), (int)response.StatusCode, reason);
        }

        return new PushResult
        {
            IsSuccess = false,
            IsDeadToken = isDeadToken,
            ErrorCode = reason,
            ErrorMessage = $"APNs returned HTTP {(int)response.StatusCode}: {reason}",
            RetryAfter = ParseRetryAfter(response)
        };
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is null)
            return null;

        if (response.Headers.RetryAfter.Delta is { } delta)
            return delta;

        if (response.Headers.RetryAfter.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : null;
        }

        return null;
    }

    private static ApnsRequest BuildPayload(PushNotification notification)
    {
        var request = new ApnsRequest();

        // Alert
        if (notification.Title is not null || notification.Body is not null || notification.Subtitle is not null)
        {
            request.Aps.Alert = new ApnsAlert
            {
                Title = notification.Title,
                Subtitle = notification.Subtitle,
                Body = notification.Body
            };
        }

        if (notification.Badge is not null)
            request.Aps.Badge = notification.Badge;

        if (notification.Sound is not null)
            request.Aps.Sound = notification.Sound;

        if (notification.ContentAvailable)
            request.Aps.ContentAvailable = 1;

        // Image requires mutable-content for notification service extension
        if (notification.ImageUrl is not null)
        {
            request.Aps.MutableContent = 1;
            request.CustomData ??= new Dictionary<string, object>();
            request.CustomData["image_url"] = notification.ImageUrl;
        }

        // Custom data
        if (notification.Data is { Count: > 0 })
        {
            request.CustomData ??= new Dictionary<string, object>();
            foreach (var kvp in notification.Data)
            {
                request.CustomData[kvp.Key] = kvp.Value;
            }
        }

        return request;
    }

    private static string MaskToken(string token) =>
        token.Length > 8 ? $"{token[..4]}...{token[^4..]}" : "***";
}
