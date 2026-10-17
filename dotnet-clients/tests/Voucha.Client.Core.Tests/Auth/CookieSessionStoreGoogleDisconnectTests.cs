using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class CookieSessionStoreGoogleDisconnectTests
{
  [Fact]
  public async Task CommittedGoogleDisconnectClearsOnlyGoogleIdentity()
  {
    var handler = new IdentityHandler();
    var client = new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    });
    using var cookieJar = new SessionCookieJar(
        new CookieContainer(),
        new Uri("https://api.test"),
        new CookiePersistence());
    var store = new CookieSessionStore(client, cookieJar);

    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal("google-1", store.Current.Identity?.GoogleAccount?.Id);
    Assert.Equal("github-1", store.Current.Identity?.GithubAccount?.Id);

    store.ApplyCommittedOAuthDisconnect(OAuthBrokerProvider.Google);

    Assert.Null(store.Current.Identity?.GoogleAccount);
    Assert.Equal("github-1", store.Current.Identity?.GithubAccount?.Id);
  }

  private sealed class IdentityHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("""
          {"identity":{"id":"user-1","username":"alice",
            "google_account":{"id":"google-1","name":"Google Alice"},
            "github_account":{"id":"github-1","name":"octocat"}}}
          """),
        });
  }

  private sealed class CookiePersistence : ISessionCookiePersistence
  {
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(string?));

    public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
