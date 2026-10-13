using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class ProtectedMemberMcpTokenStoreTests
{
  private static readonly Uri Resource = new("https://example.test/api/v1/mcp");
  private static readonly Uri Client = new("https://example.test/api/v1/oauth/native-clients/windows");
  private static readonly MemberMcpOAuthTokens Tokens =
      new("access-secret", "refresh-secret", 3600, "mcp.user:read", "Bearer");

  [Fact]
  public async Task ColdStartClearAccountRemovesEveryScopeForOnlyTheExactAccount()
  {
    var values = new FakeProtectedValues();
    var target = Scope("member-1", "https://issuer-one.test/");
    var otherIssuer = Scope("member-1", "https://issuer-two.test/");
    var prefixLikeAccount = Scope("member-10", "https://issuer-one.test/");
    using (var first = new ProtectedMemberMcpTokenStore(values))
    {
      await first.SaveAsync(target, Tokens);
      await first.SaveAsync(otherIssuer, Tokens with { AccessToken = "other-issuer" });
      await first.SaveAsync(prefixLikeAccount, Tokens with { AccessToken = "other-account" });
    }
    using var coldStart = new ProtectedMemberMcpTokenStore(values);
    await coldStart.ClearAccountAsync("member-1");
    Assert.Null(await coldStart.LoadAsync(target));
    Assert.Null(await coldStart.LoadAsync(otherIssuer));
    Assert.Equal("other-account", (await coldStart.LoadAsync(prefixLikeAccount))?.AccessToken);
  }

  [Fact]
  public async Task ConfirmedStartupKeepsOnlyTheRestoredExactAccount()
  {
    var values = new FakeProtectedValues();
    var restored = Scope("member-1", "https://issuer.test/");
    var stale = Scope("member-10", "https://issuer.test/");
    using var store = new ProtectedMemberMcpTokenStore(values);
    await store.SaveAsync(restored, Tokens);
    await store.SaveAsync(stale, Tokens with { AccessToken = "stale" });

    await store.ClearAllAccountsExceptAsync("member-1");

    Assert.Equal("access-secret", (await store.LoadAsync(restored))?.AccessToken);
    Assert.Null(await store.LoadAsync(stale));
    await store.ClearAllAccountsAsync();
    Assert.Null(await store.LoadAsync(restored));
  }

  [Fact]
  public async Task ClearingOneScopePreservesOtherIssuerAndClient()
  {
    var values = new FakeProtectedValues();
    using var store = new ProtectedMemberMcpTokenStore(values);
    var target = Scope("member-1", "https://issuer.test/");
    var otherIssuer = Scope("member-1", "https://other-issuer.test/");
    var otherClient = target with { ClientId = new Uri("https://example.test/other-client") };
    await store.SaveAsync(target, Tokens);
    await store.SaveAsync(otherIssuer, Tokens with { AccessToken = "other-issuer" });
    await store.SaveAsync(otherClient, Tokens with { AccessToken = "other-client" });

    await store.ClearAsync(target);

    Assert.Null(await store.LoadAsync(target));
    Assert.Equal("other-issuer", (await store.LoadAsync(otherIssuer))?.AccessToken);
    Assert.Equal("other-client", (await store.LoadAsync(otherClient))?.AccessToken);
  }

  [Fact]
  public async Task TakeAccountReturnsAllScopesOnlyAfterExactAccountIsRemoved()
  {
    var values = new FakeProtectedValues();
    var store = new ProtectedMemberMcpTokenStore(values);
    var current = Scope("member-1", "https://issuer.test/");
    var historical = Scope("member-1", "https://old-issuer.test/");
    var other = Scope("member-10", "https://issuer.test/");
    await store.SaveAsync(current, Tokens);
    await store.SaveAsync(historical, Tokens with { RefreshToken = "old-refresh" });
    await store.SaveAsync(other, Tokens with { AccessToken = "other-account" });

    var taken = await store.TakeAccountAsync("member-1");
    store.Dispose();

    Assert.Equal(2, taken.Count);
    Assert.Contains(taken, grant => grant.Scope == historical && grant.Tokens.RefreshToken == "old-refresh");
    Assert.Null(await store.LoadAsync(current));
    Assert.Null(await store.LoadAsync(historical));
    Assert.Equal("other-account", (await store.LoadAsync(other))?.AccessToken);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task InterruptedSaveNeverLeavesAnUnindexedCredential(int failedWrite)
  {
    var values = new FakeProtectedValues { FailWriteNumber = failedWrite };
    using var store = new ProtectedMemberMcpTokenStore(values);
    var scope = Scope("member-1", "https://issuer.test/");
    await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(scope, Tokens));
    values.FailWriteNumber = null;

    using var coldStart = new ProtectedMemberMcpTokenStore(values);
    await coldStart.ClearAllAccountsAsync();
    Assert.Null(await coldStart.LoadAsync(scope));
    Assert.DoesNotContain(values.Data.Values, value => value.Contains("access-secret", StringComparison.Ordinal));
  }

  [Fact]
  public async Task InterruptedCleanupKeepsAccountIndexedForColdStartRetry()
  {
    var values = new FakeProtectedValues();
    var scope = Scope("member-1", "https://issuer.test/");
    using var store = new ProtectedMemberMcpTokenStore(values);
    await store.SaveAsync(scope, Tokens);
    values.FailRemoveNumber = 1;
    await Assert.ThrowsAsync<IOException>(() => store.ClearAccountAsync("member-1"));
    values.FailRemoveNumber = null;

    using var coldStart = new ProtectedMemberMcpTokenStore(values);
    await coldStart.ClearAllAccountsAsync();
    Assert.Null(await coldStart.LoadAsync(scope));
    Assert.DoesNotContain(values.Data.Values, value => value.Contains("refresh-secret", StringComparison.Ordinal));
  }

  private static MemberMcpOAuthTokenScope Scope(string account, string issuer) =>
      new(account, issuer, Resource, Client);

  private sealed class FakeProtectedValues : IMemberMcpProtectedValues
  {
    public Dictionary<string, string> Data { get; } = new(StringComparer.Ordinal);
    public int? FailWriteNumber { get; set; }
    public int? FailRemoveNumber { get; set; }
    private int writes;
    private int removes;

    public Task<string?> ReadAsync(string key) =>
        Task.FromResult(Data.GetValueOrDefault(key));

    public Task WriteAsync(string key, string value)
    {
      if (++writes == FailWriteNumber) throw new IOException("simulated protected write failure");
      Data[key] = value;
      return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
      if (++removes == FailRemoveNumber) throw new IOException("simulated protected remove failure");
      Data.Remove(key);
      return Task.CompletedTask;
    }
  }
}
