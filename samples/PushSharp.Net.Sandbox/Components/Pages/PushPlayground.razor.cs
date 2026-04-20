using Microsoft.AspNetCore.Components;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Models;
using PushSharp.Net.Registration;

namespace PushSharp.Net.Sandbox.Components.Pages;

public partial class PushPlayground
{
    [Inject] private IPushClient PushClient { get; set; } = default!;
    [Inject] private IDeviceRegistrationRepository Store { get; set; } = default!;

    private readonly List<string> _log = [];
    private bool _busy;

    private string? _regUserId, _regToken, _regTags;
    private string? _bulkRegLines;

    private string? _oneToken, _oneTitle = "Test", _oneBody = "Hello from PushSharp.Net";
    private string? _batchTokens, _batchTitle = "Batch", _batchBody = "Hello (batch)";

    private string? _userOneId, _userOneTitle = "User", _userOneBody;
    private string? _userManyIds, _userManyTitle = "Users", _userManyBody;

    private string? _tagOne, _tagOneTitle = "Tag", _tagOneBody;
    private string? _tagMany, _tagManyTitle = "Tags", _tagManyBody;

    private string? _allTitle = "Broadcast", _allBody = "Hello everyone!";

    private void Log(string line)
    {
        _log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
        if (_log.Count > 400)
            _log.RemoveRange(400, _log.Count - 400);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private static IEnumerable<string> SplitLines(string? s) =>
        (s ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);

    private static IEnumerable<string> SplitCommaOrLines(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        foreach (var part in s.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0)
                yield return part;
        }
    }

    private Task RegisterOneAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_regToken))
        {
            Log("Registration: enter a token.");
            return;
        }

        var tags = SplitCommaOrLines(_regTags).ToArray();
        await Store.SaveAsync(new DeviceRegistration
        {
            DeviceId = Guid.NewGuid().ToString(),
            DeviceToken = _regToken.Trim(),
            Platform = "fcm",
            UserId = string.IsNullOrWhiteSpace(_regUserId) ? null : _regUserId.Trim(),
            Tags = tags
        }, default);
        Log($"Registered token (user={_regUserId ?? "n/a"}, tags: {string.Join(", ", tags)})");
    });

    private Task RegisterBulkAsync() => RunAsync(async () =>
    {
        var lines = SplitLines(_bulkRegLines).ToList();
        if (lines.Count == 0)
        {
            Log("Bulk: no lines.");
            return;
        }

        var n = 0;
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                Log($"Skipped (expected user|token|tags): {line[..Math.Min(40, line.Length)]}");
                continue;
            }

            var userId = string.IsNullOrEmpty(parts[0]) ? null : parts[0];
            var token = parts[1];
            var tags = parts.Length > 2
                ? SplitCommaOrLines(parts[2]).ToArray()
                : Array.Empty<string>();

            await Store.SaveAsync(new DeviceRegistration
            {
                DeviceId = Guid.NewGuid().ToString(),
                DeviceToken = token,
                Platform = "fcm",
                UserId = userId,
                Tags = tags
            }, default);
            n++;
        }

        Log($"Bulk registration: {n} device(s).");
    });

    private Task SendOneTokenAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_oneToken))
        {
            Log("Token is empty.");
            return;
        }

        var r = await PushClient.SendAsync(_oneToken.Trim(), Build(_oneTitle, _oneBody));
        Log(FormatSingle(_oneToken!, r));
    });

    private Task SendBatchTokensAsync() => RunAsync(async () =>
    {
        var tokens = SplitLines(_batchTokens).ToList();
        if (tokens.Count == 0)
        {
            Log("Batch: add tokens (one per line).");
            return;
        }

        var batch = await PushClient.SendBatchAsync(tokens, Build(_batchTitle, _batchBody));
        Log($"Batch: success={batch.SuccessCount}, failures={batch.FailureCount}");
        foreach (var row in batch.Results)
            Log($"  {FormatSingle(row.DeviceToken, row.Result)}");
    });

    private Task SendToUserOneAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_userOneId))
        {
            Log("User ID is empty.");
            return;
        }

        var batch = await PushClient.SendToUserAsync(_userOneId.Trim(), Build(_userOneTitle, _userOneBody));
        Log($"User '{_userOneId}': success={batch.SuccessCount}, failures={batch.FailureCount}");
    });

    private Task SendToManyUsersAsync() => RunAsync(async () =>
    {
        var ids = SplitCommaOrLines(_userManyIds).Distinct().ToList();
        if (ids.Count == 0)
        {
            Log("User ID list is empty.");
            return;
        }

        var note = Build(_userManyTitle, _userManyBody);
        foreach (var id in ids)
        {
            var batch = await PushClient.SendToUserAsync(id, note);
            Log($"User '{id}': success={batch.SuccessCount}, failures={batch.FailureCount}");
        }
    });

    private Task SendByOneTagAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_tagOne))
        {
            Log("Tag is empty.");
            return;
        }

        var batch = await PushClient.SendByTagAsync(_tagOne.Trim(), Build(_tagOneTitle, _tagOneBody));
        Log($"Tag '{_tagOne}': success={batch.SuccessCount}, failures={batch.FailureCount}");
    });

    private Task SendByManyTagsAsync() => RunAsync(async () =>
    {
        var tags = SplitCommaOrLines(_tagMany).Distinct().ToList();
        if (tags.Count == 0)
        {
            Log("Tag list is empty.");
            return;
        }

        var note = Build(_tagManyTitle, _tagManyBody);
        foreach (var tag in tags)
        {
            var batch = await PushClient.SendByTagAsync(tag, note);
            Log($"Tag '{tag}': success={batch.SuccessCount}, failures={batch.FailureCount}");
        }
    });

    private Task SendToAllAsync() => RunAsync(async () =>
    {
        var batch = await PushClient.SendToAllAsync(Build(_allTitle, _allBody));
        Log($"To all: success={batch.SuccessCount}, failures={batch.FailureCount}");
    });

    private static PushNotification Build(string? title, string? body) => new()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "PushSharp" : title.Trim(),
        Body = string.IsNullOrWhiteSpace(body) ? "(no body)" : body.Trim()
    };

    private static string FormatSingle(string token, PushResult r)
    {
        var masked = token.Length > 12 ? $"{token[..6]}…{token[^4..]}" : token;
        return r.IsSuccess
            ? $"OK {masked}"
            : $"FAIL {masked} dead={r.IsDeadToken} code={r.ErrorCode} msg={r.ErrorMessage}";
    }
}
