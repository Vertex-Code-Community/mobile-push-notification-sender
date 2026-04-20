using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PushSharp.Net.Providers.Apns;
using Xunit;

namespace PushSharp.Net.Tests;

public class ApnsJwtProviderTests
{
    private static string GenerateTestP8Key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyBytes = ecdsa.ExportPkcs8PrivateKey();
        var base64 = Convert.ToBase64String(keyBytes);
        return $"-----BEGIN PRIVATE KEY-----\n{base64}\n-----END PRIVATE KEY-----";
    }

    private static ApnsJwtProvider CreateProvider(string? keyContent = null)
    {
        keyContent ??= GenerateTestP8Key();

        var options = Options.Create(new ApnsOptions
        {
            KeyId = "TESTKEY123",
            TeamId = "TEAMID1234",
            BundleId = "com.test.app",
            PrivateKeyContent = keyContent
        });

        return new ApnsJwtProvider(options, NullLogger<ApnsJwtProvider>.Instance);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsValidJwtFormat()
    {
        using var provider = CreateProvider();

        var token = await provider.GetTokenAsync(CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "JWT must have header.payload.signature");
    }

    [Fact]
    public async Task GetTokenAsync_HeaderContainsES256AndKeyId()
    {
        using var provider = CreateProvider();

        var token = await provider.GetTokenAsync(CancellationToken.None);
        var headerJson = DecodeBase64Url(token.Split('.')[0]);

        headerJson.Should().Contain("\"alg\":\"ES256\"");
        headerJson.Should().Contain("\"kid\":\"TESTKEY123\"");
    }

    [Fact]
    public async Task GetTokenAsync_PayloadContainsTeamIdAndIat()
    {
        using var provider = CreateProvider();

        var token = await provider.GetTokenAsync(CancellationToken.None);
        var payloadJson = DecodeBase64Url(token.Split('.')[1]);

        payloadJson.Should().Contain("\"iss\":\"TEAMID1234\"");
        payloadJson.Should().Contain("\"iat\":");
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsCachedToken()
    {
        using var provider = CreateProvider();

        var token1 = await provider.GetTokenAsync(CancellationToken.None);
        var token2 = await provider.GetTokenAsync(CancellationToken.None);

        token1.Should().Be(token2, "second call should return cached token");
    }

    [Fact]
    public async Task GetTokenAsync_SignatureIsVerifiable()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyBytes = ecdsa.ExportPkcs8PrivateKey();
        var pem = $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(keyBytes)}\n-----END PRIVATE KEY-----";

        using var provider = CreateProvider(pem);
        var token = await provider.GetTokenAsync(CancellationToken.None);

        var parts = token.Split('.');
        var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = DecodeBase64UrlBytes(parts[2]);

        var isValid = ecdsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256);
        isValid.Should().BeTrue("signature should be verifiable with the same key");
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static byte[] DecodeBase64UrlBytes(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
