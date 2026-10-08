using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class LocalizationRefreshConcurrencyTests
{
  [Fact]
  public async Task RefreshForAnotherLocaleDoesNotWaitForTheFirstLocale()
  {
    using var handler = new HeldHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var controller = new UiLocaleController(new DeviceLanguages());
    var cache = new LocalizationValueCache();
    var service = new LocalizationRefreshService(new VouchaApiClient(http), controller, cache);
    var token = TestContext.Current.CancellationToken;
    var first = service.RefreshChromeAsync(token);
    await handler.Entered.Task.WaitAsync(token);
    controller.ApplySavedLocale("es");
    var second = service.RefreshChromeAsync(token);

    try
    {
      Assert.Equal(2, handler.Calls);
    }
    finally
    {
      handler.Release.TrySetResult();
      await Task.WhenAll(first, second);
    }

    Assert.Equal("Value 1", cache.Value("common.cancel", "en"));
    Assert.Equal("Value 2", cache.Value("common.cancel", "es"));
  }

  [Fact]
  public async Task CancelingAWaitingRefreshDoesNotCancelTheActiveRefresh()
  {
    using var handler = new HeldHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var controller = new UiLocaleController(new DeviceLanguages());
    var cache = new LocalizationValueCache();
    var service = new LocalizationRefreshService(new VouchaApiClient(http), controller, cache);
    var token = TestContext.Current.CancellationToken;
    var first = service.RefreshChromeAsync(token);
    await handler.Entered.Task.WaitAsync(token);
    using var waitingCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
    var second = service.RefreshChromeAsync(waitingCancellation.Token);

    try
    {
      await waitingCancellation.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }
    finally
    {
      handler.Release.TrySetResult();
      await first;
    }

    await service.RefreshChromeAsync(token);
    Assert.Equal(1, handler.Calls);
    Assert.Equal("Value 1", cache.Value("common.cancel", "en"));
  }

  [Fact]
  public async Task OverlappingRefreshesForOneLocaleShareTheFreshRepresentation()
  {
    using var handler = new HeldHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var controller = new UiLocaleController(new DeviceLanguages());
    var cache = new LocalizationValueCache();
    var service = new LocalizationRefreshService(new VouchaApiClient(http), controller, cache);
    var token = TestContext.Current.CancellationToken;
    var first = service.RefreshChromeAsync(token);
    await handler.Entered.Task.WaitAsync(token);
    var second = service.RefreshChromeAsync(token);

    try
    {
      Assert.Equal(1, handler.Calls);
    }
    finally
    {
      handler.Release.TrySetResult();
      await Task.WhenAll(first, second);
    }

    Assert.Equal(1, handler.Calls);
    Assert.Equal("Value 1", cache.Value("common.cancel", "en"));
    Assert.Equal("\"rev-1\"", cache.Etag("en"));
  }

  private sealed class HeldHandler : HttpMessageHandler
  {
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var call = Interlocked.Increment(ref calls);
      if (call == 1)
      {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
      }
      return new(HttpStatusCode.OK)
      {
        RequestMessage = request,
        Content = new StringContent($$"""
            { "contract": "v1", "revision": "rev-{{call}}", "ttlSeconds": 120,
              "messages": { "common.cancel": "Value {{call}}" } }
            """),
      };
    }
  }

  private sealed class DeviceLanguages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en-US"];
  }
}
