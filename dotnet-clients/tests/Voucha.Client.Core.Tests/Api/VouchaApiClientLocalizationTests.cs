using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientLocalizationTests
{
  [Fact]
  public async Task FetchLocalizationAsyncUsesPublicQueryAndDecodesBatch()
  {
    var handler = new RecordingHandler("""
        {
          "contract": "v1",
          "revision": "rev-1",
          "ttlSeconds": 300,
          "messages": { "common.cancel": "Abort" }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchLocalizationAsync(
        "dotnet",
        "en",
        "common.*",
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.NotNull(handler.PathAndQuery);
    Assert.StartsWith("/api/v1/localization?", handler.PathAndQuery);
    Assert.Contains("consumer=dotnet", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("locales=en", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("selectors=", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.NotNull(response);
    Assert.Equal("v1", response.Contract);
    Assert.Equal("rev-1", response.Revision);
    Assert.Equal(300, response.TtlSeconds);
    Assert.Equal("Abort", LocalizationLeafFlatten.Flatten(response.Messages)["common.cancel"]);
  }

  [Fact]
  public async Task FetchLocalizationAsyncReturnsNullOnNotModified()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.NotModified);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchLocalizationAsync(
        "dotnet",
        "en",
        "common.*",
        "\"rev-1\"",
        TestContext.Current.CancellationToken);

    Assert.Null(response);
    Assert.NotNull(handler.PathAndQuery);
    Assert.StartsWith("/api/v1/localization?", handler.PathAndQuery);
  }

  private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
      : HttpMessageHandler
  {
    public HttpMethod? Method { get; private set; }
    public string? PathAndQuery { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Method = request.Method;
      PathAndQuery = request.RequestUri?.PathAndQuery;
      return Task.FromResult(new HttpResponseMessage(status)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      });
    }
  }
}
