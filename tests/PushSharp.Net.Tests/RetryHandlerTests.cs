using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PushSharp.Net.Internal;
using PushSharp.Net.Models;
using Xunit;

namespace PushSharp.Net.Tests;

public class RetryHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_Success_ReturnsImmediately()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                return Task.FromResult(new PushResult { IsSuccess = true });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_DeadToken_DoesNotRetry()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                return Task.FromResult(new PushResult
                {
                    IsSuccess = false,
                    IsDeadToken = true,
                    ErrorCode = "UNREGISTERED"
                });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        result.IsDeadToken.Should().BeTrue();
        callCount.Should().Be(1, "dead token should not be retried");
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFcmError_DoesNotRetry()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                return Task.FromResult(new PushResult
                {
                    IsSuccess = false,
                    ErrorCode = "INVALID_ARGUMENT"
                });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        callCount.Should().Be(1, "permanent error should not be retried");
        result.ErrorCode.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task ExecuteAsync_PermanentApnsError_DoesNotRetry()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                return Task.FromResult(new PushResult
                {
                    IsSuccess = false,
                    ErrorCode = "BadDeviceToken",
                    IsDeadToken = true
                });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransientThenSuccess_RetriesAndSucceeds()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                if (callCount <= 2)
                    return Task.FromResult(new PushResult
                    {
                        IsSuccess = false,
                        ErrorCode = "UNAVAILABLE"
                    });
                return Task.FromResult(new PushResult { IsSuccess = true });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_NullErrorCode_DoesNotRetry()
    {
        var callCount = 0;

        var result = await RetryHandler.ExecuteAsync(
            () =>
            {
                callCount++;
                return Task.FromResult(new PushResult
                {
                    IsSuccess = false,
                    ErrorCode = null
                });
            },
            NullLogger.Instance,
            "test",
            CancellationToken.None);

        callCount.Should().Be(1, "null error code = unknown error, don't retry");
    }
}
