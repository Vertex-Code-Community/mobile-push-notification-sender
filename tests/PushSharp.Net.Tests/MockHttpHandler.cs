using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PushSharp.Net.Tests;

/// <summary>
/// Test helper: mock HttpMessageHandler that returns a preconfigured response
/// and captures request details before the request is disposed.
/// </summary>
internal class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public MockHttpHandler(HttpResponseMessage response)
        : this(_ => response) { }

    public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        return _handler(request);
    }

    public static MockHttpHandler WithJson(HttpStatusCode statusCode, string json)
    {
        return new MockHttpHandler(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public static MockHttpHandler WithSuccess(string responseJson = "{\"name\":\"projects/test/messages/123\"}")
    {
        return WithJson(HttpStatusCode.OK, responseJson);
    }
}
