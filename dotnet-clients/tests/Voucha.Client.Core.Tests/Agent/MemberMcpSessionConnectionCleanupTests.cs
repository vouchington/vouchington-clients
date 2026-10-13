using System.Net;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionConnectionCleanupTests
{
  [Fact]
  public async Task DetachedCleanupRevokesOnlyCapturedCurrentIssuerGrantWithoutProtectedStoreReads()
  {
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata("https://issuer.test/",
        new Uri("https://example.test/oauth/authorize"),
        new Uri("https://example.test/oauth/token"),
        new Uri("https://example.test/oauth/revoke"));
    var revoked = new List<string>();
    var current = new MemberMcpOAuthTokenScope("account-a", metadata.IssuerIdentifier,
        new Uri(origin, "/api/v1/mcp"), new Uri(origin, "/api/v1/oauth/native-clients/windows"));
    var grants = new[]
    {
      new MemberMcpStoredGrant(current,
          new MemberMcpOAuthTokens("current-access", "current-refresh", 3600, "mcp.user:read", "Bearer")),
      new MemberMcpStoredGrant(current with { IssuerIdentifier = "https://old-issuer.test/" },
          new MemberMcpOAuthTokens("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer")),
      new MemberMcpStoredGrant(current,
          new MemberMcpOAuthTokens("latest-access", "latest-refresh", 3600, "mcp.user:read", "Bearer"))
    };
    var store = new NoStore();
    var connection = new MemberMcpSessionConnection(origin, "account-a", metadata, store,
        new NoBrowser(), () => new RevokeHandler(revoked));

    await connection.StartRemoteCleanup(grants).WaitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["latest-refresh"], revoked);
  }

  private sealed class RevokeHandler(List<string> revoked) : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.Equal("/oauth/revoke", request.RequestUri?.AbsolutePath);
      var form = await request.Content!.ReadAsStringAsync(cancellationToken);
      var refresh = Uri.UnescapeDataString(form.Split("token=", StringSplitOptions.None)[1]);
      revoked.Add(refresh);
      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }

  private sealed class NoBrowser : IMemberMcpSystemBrowser
  {
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Browser should not open during remote cleanup.");
  }

  private sealed class NoStore : IMemberMcpAccountTokenStore
  {
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        throw new InvalidOperationException("Protected store must not be read after local deletion.");
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens) => throw new NotSupportedException();
    public Task ClearAsync(MemberMcpOAuthTokenScope scope) => throw new NotSupportedException();
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri) => throw new NotSupportedException();
    public Task ClearPendingAsync(string accountId) => throw new NotSupportedException();
    public Task<bool> HasAccountGrantAsync(string accountId) => throw new NotSupportedException();
    public Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId) => throw new NotSupportedException();
    public Task ClearAccountAsync(string accountId) => throw new NotSupportedException();
    public Task ClearAllAccountsAsync() => throw new NotSupportedException();
    public Task ClearAllAccountsExceptAsync(string? accountId) => throw new NotSupportedException();
  }
}
