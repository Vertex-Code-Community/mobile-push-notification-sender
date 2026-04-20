using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Models;

namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// FCM HTTP v1 provider implementation.
/// Posts to <c>https://fcm.googleapis.com/v1/projects/{id}/messages:send</c>.
/// </summary>
internal sealed class FcmProvider : IPushProvider
{
    private const string FcmBaseUrl = "https://fcm.googleapis.com/v1/projects/";
    private const string DefaultChannelId = "default";

    private static readonly HashSet<string> PermanentErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INVALID_ARGUMENT",
        "SENDER_ID_MISMATCH",
        "THIRD_PARTY_AUTH_ERROR"
    };

    private static readonly HashSet<string> DeadTokenErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNREGISTERED"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFcmTokenProvider _tokenProvider;
    private readonly string _sendUrl;
    private readonly ILogger<FcmProvider> _logger;

    public FcmProvider(
        IHttpClientFactory httpClientFactory,
        IFcmTokenProvider tokenProvider,
        IOptions<FcmOptions> options,
        ILogger<FcmProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _logger = logger;
        _sendUrl = $"{FcmBaseUrl}{options.Value.ProjectId}/messages:send";
        _logger.LogInformation("FCM provider initialized for project: {ProjectId}", options.Value.ProjectId);
    }

    public bool CanHandle(string deviceToken)
    {
        if (IsTopicOrCondition(deviceToken))
            return true;

        return Internal.TokenDetector.Detect(deviceToken) == PushProvider.Fcm;
    }

    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        var request = BuildFcmRequest(deviceToken, notification);
        var json = JsonSerializer.Serialize(request, Providers.Fcm.FcmJsonContext.Default.FcmRequest);

        var httpClient = _httpClientFactory.CreateClient("PushSharp.Fcm");
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _sendUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed with exception for token: {Token}", MaskToken(deviceToken));
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
        string? fcmErrorCode = null;
        string? errorMessage = null;

        try
        {
            var errorResponse = JsonSerializer.Deserialize(responseBody, Providers.Fcm.FcmJsonContext.Default.FcmErrorResponse);
            errorMessage = errorResponse?.Error?.Message;

            // FCM v1 puts the specific error code in details[].errorCode
            if (errorResponse?.Error?.Details is { Count: > 0 })
            {
                fcmErrorCode = errorResponse.Error.Details
                    .FirstOrDefault(d => !string.IsNullOrEmpty(d.ErrorCode))?.ErrorCode;
            }

            fcmErrorCode ??= errorResponse?.Error?.Status;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Could not parse FCM error response for token: {Token}", MaskToken(deviceToken));
        }

        var isDeadToken = fcmErrorCode is not null && DeadTokenErrorCodes.Contains(fcmErrorCode);

        if (isDeadToken)
        {
            _logger.LogInformation("FCM dead token detected: {Token}, code: {ErrorCode}",
                MaskToken(deviceToken), fcmErrorCode);
        }
        else if (fcmErrorCode is not null && PermanentErrorCodes.Contains(fcmErrorCode))
        {
            _logger.LogWarning("FCM permanent error for token: {Token}, code: {ErrorCode}, message: {Message}",
                MaskToken(deviceToken), fcmErrorCode, errorMessage);
        }
        else
        {
            // Transient: QUOTA_EXCEEDED, INTERNAL, UNAVAILABLE, or unknown
            _logger.LogWarning(
                "FCM transient error for token: {Token}, status: {StatusCode}, code: {ErrorCode}, message: {Message}",
                MaskToken(deviceToken), (int)response.StatusCode, fcmErrorCode, errorMessage);
        }

        return new PushResult
        {
            IsSuccess = false,
            IsDeadToken = isDeadToken,
            ErrorCode = fcmErrorCode,
            ErrorMessage = errorMessage,
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

    private static FcmRequest BuildFcmRequest(string deviceToken, PushNotification notification)
    {
        var message = new FcmMessage();

        // Route: topic, condition, or device token
        if (deviceToken.StartsWith("/topics/", StringComparison.Ordinal))
        {
            message.Topic = deviceToken["/topics/".Length..];
        }
        else if (deviceToken.Contains("&&") || deviceToken.Contains("||")
                 || deviceToken.StartsWith("'", StringComparison.Ordinal))
        {
            message.Condition = deviceToken;
        }
        else
        {
            message.Token = deviceToken;
        }

        // Notification payload
        if (notification.Title is not null || notification.Body is not null || notification.ImageUrl is not null)
        {
            message.Notification = new FcmNotification
            {
                Title = notification.Title,
                Body = notification.Body,
                Image = notification.ImageUrl
            };
        }

        // Custom data
        if (notification.Data is { Count: > 0 })
        {
            message.Data = new Dictionary<string, string>(notification.Data);
        }

        // Android config — always set channel_id (Android 8+ requirement)
        var androidNotification = new FcmAndroidNotification
        {
            ChannelId = DefaultChannelId,
            Sound = notification.Sound,
            Image = notification.ImageUrl
        };

        if (notification.Badge is not null)
        {
            androidNotification.NotificationCount = notification.Badge;
        }

        string? ttl = null;
        if (notification.Expiration is { } expiration)
        {
            var duration = expiration - DateTimeOffset.UtcNow;
            if (duration > TimeSpan.Zero)
            {
                ttl = $"{(int)duration.TotalSeconds}s";
            }
        }

        message.Android = new FcmAndroidConfig
        {
            CollapseKey = notification.CollapseKey,
            Ttl = ttl,
            Notification = androidNotification
        };

        return new FcmRequest { Message = message };
    }

    private static bool IsTopicOrCondition(string token) =>
        token.StartsWith("/topics/", StringComparison.Ordinal)
        || token.Contains("&&")
        || token.Contains("||")
        || token.StartsWith("'", StringComparison.Ordinal);

    private static string MaskToken(string token) =>
        token.Length > 8 ? $"{token[..4]}...{token[^4..]}" : "***";
}
