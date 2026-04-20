using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PushSharp.Net.Models;
using PushSharp.Net.Providers.Apns;
using Xunit;

namespace PushSharp.Net.Tests;

public class ApnsProviderTests
{
    private const string ValidApnsToken = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

    private static ApnsProvider CreateProvider(
        MockHttpHandler handler,
        string bundleId = "com.test.app",
        bool useSandbox = false)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("PushSharp.Apns"))
            .Returns(new HttpClient(handler));

        var jwtProvider = new Mock<IApnsJwtProvider>();
        jwtProvider
            .Setup(j => j.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-jwt-token");

        var options = Options.Create(new ApnsOptions
        {
            KeyId = "TESTKEY123",
            TeamId = "TEAMID1234",
            BundleId = bundleId,
            PrivateKeyContent = "test",
            UseSandbox = useSandbox
        });

        return new ApnsProvider(
            httpClientFactory.Object,
            jwtProvider.Object,
            options,
            NullLogger<ApnsProvider>.Instance);
    }

    [Fact]
    public async Task SendAsync_Success_ReturnsIsSuccessTrue()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Hello", Body = "World" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.IsDeadToken.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_SetsRequiredHeaders()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler, bundleId: "com.example.app");

        await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test", Body = "Body" },
            CancellationToken.None);

        var req = handler.LastRequest!;
        req.Headers.GetValues("apns-topic").Should().Contain("com.example.app");
        req.Headers.GetValues("apns-push-type").Should().Contain("alert");
        req.Headers.GetValues("apns-priority").Should().Contain("10");
    }

    [Fact]
    public async Task SendAsync_BackgroundPush_SetsCorrectHeaders()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler);

        await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { ContentAvailable = true },
            CancellationToken.None);

        var req = handler.LastRequest!;
        req.Headers.GetValues("apns-push-type").Should().Contain("background");
        req.Headers.GetValues("apns-priority").Should().Contain("5");
    }

    [Fact]
    public async Task SendAsync_UsesHttp2()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler);

        await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequest!.Version.Should().Be(new Version(2, 0));
    }

    [Fact]
    public async Task SendAsync_BadDeviceToken_ReturnsDeadToken()
    {
        var handler = MockHttpHandler.WithJson(
            HttpStatusCode.BadRequest,
            """{"reason":"BadDeviceToken"}""");
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsDeadToken.Should().BeTrue();
        result.ErrorCode.Should().Be("BadDeviceToken");
    }

    [Fact]
    public async Task SendAsync_Http410_ReturnsDeadToken()
    {
        var handler = MockHttpHandler.WithJson(
            HttpStatusCode.Gone,
            """{"reason":"Unregistered"}""");
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsDeadToken.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_ProductionEndpoint_UsesCorrectUrl()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler, useSandbox: false);

        await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequest!.RequestUri!.Host.Should().Be("api.push.apple.com");
    }

    [Fact]
    public async Task SendAsync_SandboxEndpoint_UsesCorrectUrl()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler, useSandbox: true);

        await provider.SendAsync(
            ValidApnsToken,
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequest!.RequestUri!.Host.Should().Be("api.sandbox.push.apple.com");
    }

    [Fact]
    public void CanHandle_ApnsToken_ReturnsTrue()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler);

        provider.CanHandle(ValidApnsToken).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_FcmToken_ReturnsFalse()
    {
        var handler = MockHttpHandler.WithJson(HttpStatusCode.OK, "");
        var provider = CreateProvider(handler);

        provider.CanHandle("dMw5FFRZSk-JfFBqsuaKfQ:APA91bH").Should().BeFalse();
    }
}
