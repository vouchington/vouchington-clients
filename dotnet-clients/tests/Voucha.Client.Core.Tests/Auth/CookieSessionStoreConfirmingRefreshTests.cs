using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class CookieSessionStoreConfirmingRefreshTests
{
  [Fact]
  public async Task ConfirmingRefreshPropagatesTransientFailureWithoutChangingForcedRefresh()
  {
    var handler = new OfflineHandler();
    var client = new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    });
    using var cookieJar = new SessionCookieJar(
        new CookieContainer(),
        new Uri("https://api.test"),
        new UnusedCookiePersistence());
    var store = new CookieSessionStore(client, cookieJar);

    await ((IForcedSessionRefreshStore)store)
        .RefreshAsync(force: true, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        ((IConfirmingSessionRefreshStore)store)
            .RefreshConfirmingAsync(TestContext.Current.CancellationToken));
    Assert.Equal(2, handler.RequestCount);
    Assert.False(store.Current.IsAuthenticated);
  }

  private sealed class OfflineHandler : HttpMessageHandler
  {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RequestCount++;
      return Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
    }
  }

  private sealed class UnusedCookiePersistence : ISessionCookiePersistence
  {
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(string?));

    public Task WriteAsync(
        string serializedCookies,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
