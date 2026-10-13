using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionConnectionTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BrowserOnlyStartsAfterProofIsProtectedAndListenerIsReady(bool failProtectedWrite)
  {
    var store = new FakeStore { FailPendingWrite = failProtectedWrite };
    var browser = new FakeBrowser(store);
    await using var connection = new MemberMcpSessionConnection(
        new Uri("https://example.test"), "account-a",
        new MemberMcpOAuthMetadata(
            "https://example.test/", new Uri("https://example.test/oauth/authorize"),
            new Uri("https://example.test/oauth/token"), new Uri("https://example.test/oauth/revoke")),
        store, browser);

    await Assert.ThrowsAsync<IOException>(() => connection.ConnectAndListToolsAsync(
        TestContext.Current.CancellationToken));

    Assert.Equal(failProtectedWrite ? 0 : 1, browser.Starts);
    Assert.Null(store.PendingState);
    Assert.Equal(failProtectedWrite ? 0 : 1, store.PendingClears);
  }

  private sealed class FakeBrowser(FakeStore store) : IMemberMcpSystemBrowser
  {
    public int Starts { get; private set; }
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken)
    {
      Assert.NotNull(store.PendingState);
      Assert.NotNull(store.PendingVerifier);
      Assert.NotNull(store.PendingRedirect);
      Assert.True(store.PendingRedirect.Port > 0);
      Assert.Contains(store.PendingRedirect.Host, new[] { "127.0.0.1", "[::1]", "::1" });
      Starts++;
      throw new IOException("Stop before browser return");
    }
  }

  private sealed class FakeStore : IMemberMcpAccountTokenStore
  {
    public bool FailPendingWrite { get; set; }
    public string? PendingState { get; private set; }
    public string? PendingVerifier { get; private set; }
    public Uri? PendingRedirect { get; private set; }
    public int PendingClears { get; private set; }
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        Task.FromResult<MemberMcpOAuthTokens?>(null);
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens) =>
        Task.CompletedTask;
    public Task ClearAsync(MemberMcpOAuthTokenScope scope) => Task.CompletedTask;
    public Task<bool> HasAccountGrantAsync(string accountId) => Task.FromResult(false);
    public Task ClearAccountAsync(string accountId) => Task.CompletedTask;
    public Task ClearAllAccountsAsync() => Task.CompletedTask;
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri)
    {
      if (FailPendingWrite) throw new IOException("Protected store failed");
      PendingState = state;
      PendingVerifier = verifier;
      PendingRedirect = redirectUri;
      return Task.CompletedTask;
    }
    public Task ClearPendingAsync(string accountId)
    {
      PendingState = null;
      PendingVerifier = null;
      PendingRedirect = null;
      PendingClears++;
      return Task.CompletedTask;
    }
  }
}
