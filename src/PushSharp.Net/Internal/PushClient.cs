using Microsoft.Extensions.Logging;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Models;
using PushSharp.Net.Registration;

namespace PushSharp.Net.Internal;

/// <summary>
/// Default implementation of <see cref="IPushClient"/>.
/// Routes notifications to the appropriate provider based on device token format.
/// Wraps provider calls with transient retry (3 attempts, exponential backoff).
/// </summary>
internal sealed class PushClient : IPushClient
{
    private readonly IEnumerable<IPushProvider> _providers;
    private readonly IDeviceRegistrationRepository? _store;
    private readonly ILogger<PushClient> _logger;

    public PushClient(
        IEnumerable<IPushProvider> providers,
        ILogger<PushClient> logger,
        IDeviceRegistrationRepository? store = null)
    {
        _providers = providers;
        _logger = logger;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(deviceToken));

        if (provider is null)
        {
            var detected = TokenDetector.Detect(deviceToken);
            throw new InvalidOperationException(
                $"No push provider is configured for {detected} token format. " +
                "Call AddPushNotifications() with the appropriate provider options.");
        }

        var maskedToken = deviceToken.Length > 8
            ? $"{deviceToken[..4]}...{deviceToken[^4..]}"
            : "***";

        return await RetryHandler.ExecuteAsync(
            () => provider.SendAsync(deviceToken, notification, cancellationToken),
            _logger,
            maskedToken,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BatchPushResult> SendBatchAsync(
        IEnumerable<string> deviceTokens,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TokenResult>();

        foreach (var token in deviceTokens)
        {
            var result = await SendAsync(token, notification, cancellationToken)
                .ConfigureAwait(false);

            results.Add(new TokenResult { DeviceToken = token, Result = result });
        }

        return BuildBatchResult(results);
    }

    /// <inheritdoc/>
    public async Task<BatchPushResult> SendToUserAsync(
        string userId,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        EnsureStoreRegistered();

        var tokens = await _store!.GetTokensByUserIdAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("SendToUser {UserId}: found {Count} device(s)", userId, tokens.Count);

        return await SendBatchAndCleanup(tokens, notification, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BatchPushResult> SendByTagAsync(
        string tag,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        EnsureStoreRegistered();

        var tokens = await _store!.GetTokensByTagAsync(tag, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("SendByTag '{Tag}': found {Count} device(s)", tag, tokens.Count);

        return await SendBatchAndCleanup(tokens, notification, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BatchPushResult> SendToAllAsync(
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        EnsureStoreRegistered();

        var tokens = await _store!.GetAllTokensAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("SendToAll: found {Count} device(s)", tokens.Count);

        return await SendBatchAndCleanup(tokens, notification, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BatchPushResult> SendBatchAndCleanup(
        IReadOnlyList<string> tokens,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        var results = new List<TokenResult>();

        foreach (var token in tokens)
        {
            var result = await SendAsync(token, notification, cancellationToken)
                .ConfigureAwait(false);

            results.Add(new TokenResult { DeviceToken = token, Result = result });

            if (result.IsDeadToken)
            {
                _logger.LogInformation("Removing dead token {Token}", 
                    token.Length > 8 ? $"{token[..4]}...{token[^4..]}" : "***");

                await _store!.RemoveByTokenAsync(token, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return BuildBatchResult(results);
    }

    private static BatchPushResult BuildBatchResult(List<TokenResult> results) => new()
    {
        SuccessCount = results.Count(r => r.Result.IsSuccess),
        FailureCount = results.Count(r => !r.Result.IsSuccess),
        Results = results
    };

    private void EnsureStoreRegistered()
    {
        if (_store is null)
            throw new InvalidOperationException(
                "No IDeviceRegistrationStore is registered. " +
                "Call builder.AddDeviceStore<T>() in AddPushNotifications() to use user/tag-based sending.");
    }
}
