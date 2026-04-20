using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PushSharp.Net.Abstractions;
using PushSharp.Net.DependencyInjection;
using PushSharp.Net.Models;
using PushSharp.Net.Registration;
using Xunit;

namespace PushSharp.Net.Tests;

public class DeviceStoreTests
{
    private static ServiceProvider BuildProvider(InMemoryDeviceRepository repository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<FakeProvider>();
            push.AddDeviceStore(repository);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendToUserAsync_SendsToAllUserDevices()
    {
        var store = new InMemoryDeviceRepository();
        store.Add("user1", "TOKEN_A", "fcm");
        store.Add("user1", "TOKEN_B", "fcm");
        store.Add("user2", "TOKEN_C", "fcm");

        var provider = BuildProvider(store);
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendToUserAsync("user1",
            new PushNotification { Title = "Hi", Body = "Test" });

        result.SuccessCount.Should().Be(2);
        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.DeviceToken).Should().BeEquivalentTo(["TOKEN_A", "TOKEN_B"]);
    }

    [Fact]
    public async Task SendByTagAsync_SendsToTaggedDevices()
    {
        var store = new InMemoryDeviceRepository();
        store.Add("user1", "TOKEN_A", "fcm", ["premium", "eu"]);
        store.Add("user2", "TOKEN_B", "fcm", ["free"]);
        store.Add("user3", "TOKEN_C", "fcm", ["premium"]);

        var provider = BuildProvider(store);
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendByTagAsync("premium",
            new PushNotification { Title = "Hi", Body = "Premium only" });

        result.SuccessCount.Should().Be(2);
        result.Results.Select(r => r.DeviceToken).Should().BeEquivalentTo(["TOKEN_A", "TOKEN_C"]);
    }

    [Fact]
    public async Task SendToAllAsync_SendsToEveryDevice()
    {
        var store = new InMemoryDeviceRepository();
        store.Add("user1", "TOKEN_A", "fcm");
        store.Add("user2", "TOKEN_B", "fcm");

        var provider = BuildProvider(store);
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendToAllAsync(
            new PushNotification { Title = "Broadcast", Body = "Hello everyone" });

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task DeadToken_IsAutoRemovedFromStore()
    {
        var store = new InMemoryDeviceRepository();
        store.Add("user1", "DEAD_TOKEN", "fcm");
        store.Add("user1", "TOKEN_OK", "fcm");

        var provider = BuildProvider(store);
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendToUserAsync("user1",
            new PushNotification { Title = "Test" });

        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);
        result.Results.First(r => r.DeviceToken == "DEAD_TOKEN").Result.IsDeadToken.Should().BeTrue();

        var remaining = await store.GetTokensByUserIdAsync("user1");
        remaining.Should().ContainSingle().Which.Should().Be("TOKEN_OK");
    }

    [Fact]
    public void SendToUserAsync_WithoutStore_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<FakeProvider>();
        });
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPushClient>();

        var act = () => client.SendToUserAsync("user1",
            new PushNotification { Title = "Test" });

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IDeviceRegistrationStore*");
    }

    [Fact]
    public async Task SendToUser_NoDevices_ReturnsEmptyResult()
    {
        var store = new InMemoryDeviceRepository();
        var provider = BuildProvider(store);
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendToUserAsync("nonexistent",
            new PushNotification { Title = "Test" });

        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    /// <summary>
    /// Fake provider: returns success for all tokens except those starting with "DEAD_".
    /// </summary>
    private class FakeProvider : IPushProvider
    {
        public bool CanHandle(string deviceToken) => true;

        public Task<PushResult> SendAsync(string deviceToken, PushNotification notification, CancellationToken ct)
        {
            if (deviceToken.StartsWith("DEAD_"))
                return Task.FromResult(new PushResult
                {
                    IsSuccess = false,
                    IsDeadToken = true,
                    ErrorCode = "UNREGISTERED"
                });

            return Task.FromResult(new PushResult { IsSuccess = true });
        }
    }

    /// <summary>
    /// In-memory implementation of <see cref="IDeviceRegistrationRepository"/> for testing.
    /// This is exactly what a consumer would implement with EF Core in a real app.
    /// </summary>
    private class InMemoryDeviceRepository : IDeviceRegistrationRepository
    {
        private readonly List<DeviceRegistration> _registrations = [];

        public void Add(string userId, string token, string platform, string[]? tags = null)
        {
            _registrations.Add(new DeviceRegistration
            {
                DeviceId = Guid.NewGuid().ToString(),
                DeviceToken = token,
                Platform = platform,
                UserId = userId,
                Tags = tags ?? []
            });
        }

        public Task SaveAsync(DeviceRegistration registration, CancellationToken cancellationToken = default)
        {
            _registrations.RemoveAll(r => r.DeviceId == registration.DeviceId);
            _registrations.Add(registration);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            _registrations.RemoveAll(r => r.DeviceId == deviceId);
            return Task.CompletedTask;
        }

        public Task RemoveByTokenAsync(string deviceToken, CancellationToken cancellationToken = default)
        {
            _registrations.RemoveAll(r => r.DeviceToken == deviceToken);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetTokensByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            var tokens = _registrations
                .Where(r => r.UserId == userId)
                .Select(r => r.DeviceToken)
                .ToList();
            return Task.FromResult<IReadOnlyList<string>>(tokens);
        }

        public Task<IReadOnlyList<string>> GetTokensByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            var tokens = _registrations
                .Where(r => r.Tags.Contains(tag))
                .Select(r => r.DeviceToken)
                .ToList();
            return Task.FromResult<IReadOnlyList<string>>(tokens);
        }

        public Task<IReadOnlyList<string>> GetAllTokensAsync(CancellationToken cancellationToken = default)
        {
            var tokens = _registrations.Select(r => r.DeviceToken).ToList();
            return Task.FromResult<IReadOnlyList<string>>(tokens);
        }
    }
}
