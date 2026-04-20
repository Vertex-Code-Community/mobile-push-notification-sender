using FluentAssertions;
using PushSharp.Net.Models;
using Xunit;

namespace PushSharp.Net.Tests;

public class TokenDetectorTests
{
    [Theory]
    [InlineData("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2", PushProvider.Apns)]
    [InlineData("A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2", PushProvider.Apns)]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000", PushProvider.Apns)]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", PushProvider.Apns)]
    public void Detect_64HexChars_ReturnsApns(string token, PushProvider expected)
    {
        Internal.TokenDetector.Detect(token).Should().Be(expected);
    }

    [Theory]
    [InlineData("dMw5FFRZSk-JfFBqsuaKfQ:APA91bH...")] // FCM token
    [InlineData("short")]
    [InlineData("")] // empty
    [InlineData("a1b2c3d4e5f6")] // too short hex
    [InlineData("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2ff")] // 66 chars
    [InlineData("g1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2")] // non-hex char
    public void Detect_NonApnsTokens_ReturnsFcm(string token)
    {
        Internal.TokenDetector.Detect(token).Should().Be(PushProvider.Fcm);
    }
}
