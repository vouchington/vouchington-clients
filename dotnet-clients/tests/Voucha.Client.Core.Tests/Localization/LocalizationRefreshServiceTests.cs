using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class LocalizationRefreshServiceTests
{
  [Fact]
  public async Task RefreshChromeAppliesFlattenedValuesAndNotifiesListeners()
  {
    var handler = new RecordingHandler("""
        {
          "contract": "v1",
          "revision": "rev-1",
          "ttlSeconds": 120,
          "messages": { "common.cancel": "Abort" }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var cache = new LocalizationValueCache();
    var changes = 0;
    controller.LocaleChanged += (_, _) => changes++;
    var service = new LocalizationRefreshService(client, controller, cache);

    await service.RefreshChromeAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Abort", cache.Value("common.cancel", "en"));
    Assert.Equal(1, changes);
    Assert.Contains("consumer=dotnet", handler.PathAndQuery, StringComparison.Ordinal);
    Assert.Contains("selectors=", handler.PathAndQuery, StringComparison.Ordinal);
  }

  [Fact]
  public async Task RefreshChromeKeepsOverlayWhenTheRevisionIsUnchanged()
  {
    var cache = new LocalizationValueCache();
    cache.Apply(
        "en",
        "rev-1",
        10,
        new Dictionary<string, string> { ["common.cancel"] = "Abort" },
        DateTimeOffset.UnixEpoch);
    var handler = new RecordingHandler("{}", HttpStatusCode.NotModified);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var service = new LocalizationRefreshService(client, controller, cache);

    await service.RefreshChromeAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Abort", cache.Value("common.cancel", "en"));
    Assert.False(cache.IsExpired("en", DateTimeOffset.UtcNow.AddSeconds(1)));
  }

  private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
      : HttpMessageHandler
  {
    public string? PathAndQuery { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      PathAndQuery = request.RequestUri?.PathAndQuery;
      return Task.FromResult(new HttpResponseMessage(status)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      });
    }
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
