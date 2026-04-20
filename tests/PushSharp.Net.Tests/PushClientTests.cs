using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PushSharp.Net.Abstractions;
using PushSharp.Net.Internal;
using PushSharp.Net.Models;
using Xunit;

namespace PushSharp.Net.Tests;

public class PushClientTests
{
    private static PushClient CreateClient(params IPushProvider[] providers)
    {
        return new PushClient(providers, NullLogger<PushClient>.Instance);
    }

    [Fact]
    public async Task SendAsync_RoutesToCorrectProvider()
    {
        var fcmProvider = new Mock<IPushProvider>();
        fcmProvider.Setup(p => p.CanHandle("fcm-token")).Returns(true);
        fcmProvider
            .Setup(p => p.SendAsync("fcm-token", It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult { IsSuccess = true });

        var apnsProvider = new Mock<IPushProvider>();
        apnsProvider.Setup(p => p.CanHandle("fcm-token")).Returns(false);

        var client = CreateClient(fcmProvider.Object, apnsProvider.Object);
        var result = await client.SendAsync("fcm-token", new PushNotification { Title = "Test" });

        result.IsSuccess.Should().BeTrue();
        fcmProvider.Verify(p => p.SendAsync("fcm-token", It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        apnsProvider.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_NoProviderConfigured_ThrowsInvalidOperation()
    {
        var client = CreateClient();

        var act = () => client.SendAsync("some-token", new PushNotification { Title = "Test" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No push provider is configured*");
    }

    [Fact]
    public async Task SendBatchAsync_ReturnsPerTokenResults()
    {
        var provider = new Mock<IPushProvider>();
        provider.Setup(p => p.CanHandle(It.IsAny<string>())).Returns(true);

        var callCount = 0;
        provider
            .Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 2
                    ? new PushResult { IsSuccess = false, ErrorCode = "INVALID_ARGUMENT" }
                    : new PushResult { IsSuccess = true };
            });

        var client = CreateClient(provider.Object);
        var result = await client.SendBatchAsync(
            ["token1", "token2", "token3"],
            new PushNotification { Title = "Batch" });

        result.Results.Should().HaveCount(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
    }

    [Fact]
    public async Task SendBatchAsync_OneFailure_DoesNotAbortRest()
    {
        var provider = new Mock<IPushProvider>();
        provider.Setup(p => p.CanHandle(It.IsAny<string>())).Returns(true);
        provider
            .Setup(p => p.SendAsync("bad", It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult { IsSuccess = false, IsDeadToken = true, ErrorCode = "UNREGISTERED" });
        provider
            .Setup(p => p.SendAsync("good", It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult { IsSuccess = true });

        var client = CreateClient(provider.Object);
        var result = await client.SendBatchAsync(
            ["good", "bad", "good"],
            new PushNotification { Title = "Test" });

        result.Results.Should().HaveCount(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.Results[1].Result.IsDeadToken.Should().BeTrue();
    }
}
