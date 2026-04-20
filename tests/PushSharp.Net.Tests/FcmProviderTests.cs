using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PushSharp.Net.Models;
using PushSharp.Net.Providers.Fcm;
using Xunit;

namespace PushSharp.Net.Tests;

public class FcmProviderTests
{
    private static FcmProvider CreateProvider(
        MockHttpHandler handler,
        string projectId = "test-project")
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("PushSharp.Fcm"))
            .Returns(new HttpClient(handler));

        var tokenProvider = new Mock<IFcmTokenProvider>();
        tokenProvider
            .Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-access-token");

        var options = Options.Create(new FcmOptions { ProjectId = projectId });

        return new FcmProvider(
            httpClientFactory.Object,
            tokenProvider.Object,
            options,
            NullLogger<FcmProvider>.Instance);
    }

    [Fact]
    public async Task SendAsync_Success_ReturnsIsSuccessTrue()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            "test-fcm-token",
            new PushNotification { Title = "Hello", Body = "World" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.IsDeadToken.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_Success_SendsCorrectAuthorizationHeader()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        await provider.SendAsync(
            "test-fcm-token",
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("test-access-token");
    }

    [Fact]
    public async Task SendAsync_Success_PostsToCorrectUrl()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler, "my-project");

        await provider.SendAsync(
            "test-fcm-token",
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://fcm.googleapis.com/v1/projects/my-project/messages:send");
    }

    [Fact]
    public async Task SendAsync_Unregistered_ReturnsDeadToken()
    {
        var errorJson = """
        {
          "error": {
            "code": 404,
            "message": "Requested entity was not found.",
            "status": "NOT_FOUND",
            "details": [
              {
                "@type": "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                "errorCode": "UNREGISTERED"
              }
            ]
          }
        }
        """;
        var handler = MockHttpHandler.WithJson(HttpStatusCode.NotFound, errorJson);
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            "dead-token",
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsDeadToken.Should().BeTrue();
        result.ErrorCode.Should().Be("UNREGISTERED");
    }

    [Fact]
    public async Task SendAsync_InvalidArgument_ReturnsPermanentError()
    {
        var errorJson = """
        {
          "error": {
            "code": 400,
            "message": "Invalid registration",
            "status": "INVALID_ARGUMENT",
            "details": [
              {
                "@type": "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                "errorCode": "INVALID_ARGUMENT"
              }
            ]
          }
        }
        """;
        var handler = MockHttpHandler.WithJson(HttpStatusCode.BadRequest, errorJson);
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            "bad-token",
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsDeadToken.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task SendAsync_TopicTarget_RoutesCorrectly()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            "/topics/news",
            new PushNotification { Title = "Breaking News" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        handler.LastRequestBody.Should().Contain("\"topic\":\"news\"");
        handler.LastRequestBody.Should().NotContain("\"token\":");
    }

    [Fact]
    public async Task SendAsync_ConditionTarget_RoutesCorrectly()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        var result = await provider.SendAsync(
            "'TopicA' in topics && 'TopicB' in topics",
            new PushNotification { Title = "Targeted" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        handler.LastRequestBody.Should().Contain("\"condition\":");
        handler.LastRequestBody.Should().NotContain("\"token\":");
    }

    [Fact]
    public async Task SendAsync_AlwaysIncludesChannelId()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        await provider.SendAsync(
            "test-token",
            new PushNotification { Title = "Test" },
            CancellationToken.None);

        handler.LastRequestBody.Should().Contain("\"channel_id\":\"default\"");
    }

    [Fact]
    public void CanHandle_FcmToken_ReturnsTrue()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        provider.CanHandle("dMw5FFRZSk-JfFBqsuaKfQ:APA91bH").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_ApnsToken_ReturnsFalse()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        provider.CanHandle("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2")
            .Should().BeFalse();
    }

    [Fact]
    public void CanHandle_TopicString_ReturnsTrue()
    {
        var handler = MockHttpHandler.WithSuccess();
        var provider = CreateProvider(handler);

        provider.CanHandle("/topics/news").Should().BeTrue();
    }
}
