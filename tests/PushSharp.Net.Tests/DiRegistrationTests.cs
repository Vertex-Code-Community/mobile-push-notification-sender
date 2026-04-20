using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PushSharp.Net.Abstractions;
using PushSharp.Net.DependencyInjection;
using PushSharp.Net.Models;
using Xunit;

namespace PushSharp.Net.Tests;

public class DiRegistrationTests
{
    [Fact]
    public void Builder_ResolvesIPushClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(_ => { });

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IPushClient>();

        client.Should().NotBeNull();
    }

    [Fact]
    public void Builder_AddProvider_RegistersCustomProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<StubProvider>();
        });

        var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<IPushProvider>();

        providers.Should().ContainSingle()
            .Which.Should().BeOfType<StubProvider>();
    }

    [Fact]
    public void Builder_AddProvider_Instance_RegistersProvider()
    {
        var instance = new StubProvider();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider(instance);
        });

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IPushProvider>();

        resolved.Should().ContainSingle()
            .Which.Should().BeSameAs(instance);
    }

    [Fact]
    public async Task CustomProvider_ReceivesMatchingTokens()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<StubProvider>();
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPushClient>();

        var result = await client.SendAsync("STUB:device123",
            new PushNotification { Title = "Test", Body = "Hello" });

        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().Be("stub-ok");
    }

    [Fact]
    public void CustomProvider_UnhandledToken_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<StubProvider>();
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPushClient>();

        var act = () => client.SendAsync("unknown-token-format",
            new PushNotification { Title = "Test" });

        act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MultipleProviders_RoutedByCanHandle()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications(push =>
        {
            push.AddProvider<StubProvider>();
            push.AddProvider<AnotherStubProvider>();
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPushClient>();

        var stubResult = await client.SendAsync("STUB:abc",
            new PushNotification { Title = "Test" });
        stubResult.ErrorCode.Should().Be("stub-ok");

        var anotherResult = await client.SendAsync("OTHER:xyz",
            new PushNotification { Title = "Test" });
        anotherResult.ErrorCode.Should().Be("another-ok");
    }

    [Fact]
    public void Parameterless_RegistersPushClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotifications();

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IPushClient>();

        client.Should().NotBeNull();
    }

    #pragma warning disable CS0618
    [Fact]
    public void LegacyOverload_StillWorks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushNotificationsLegacy();

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IPushClient>();

        client.Should().NotBeNull();
    }
    #pragma warning restore CS0618

    private class StubProvider : IPushProvider
    {
        public bool CanHandle(string deviceToken) => deviceToken.StartsWith("STUB:");

        public Task<PushResult> SendAsync(string deviceToken, PushNotification notification, CancellationToken ct)
        {
            return Task.FromResult(new PushResult { IsSuccess = true, ErrorCode = "stub-ok" });
        }
    }

    private class AnotherStubProvider : IPushProvider
    {
        public bool CanHandle(string deviceToken) => deviceToken.StartsWith("OTHER:");

        public Task<PushResult> SendAsync(string deviceToken, PushNotification notification, CancellationToken ct)
        {
            return Task.FromResult(new PushResult { IsSuccess = true, ErrorCode = "another-ok" });
        }
    }
}
