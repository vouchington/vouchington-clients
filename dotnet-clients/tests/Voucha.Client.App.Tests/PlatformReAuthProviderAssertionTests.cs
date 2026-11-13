using Voucha.Client.App;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class PlatformReAuthProviderAssertionTests
{
  [Fact]
  public async Task AppleReauthUsesTheSuppliedServerChallengeAndNeverStartsOrdinarySignIn()
  {
    var apple = new AppleAdapter();
    var proof = new PlatformReAuthProviderAssertion(apple);
    Assert.Equal("fresh-apple-token", await proof.GetCredentialAsync("apple", "server-challenge", TestContext.Current.CancellationToken));
    Assert.Equal("server-challenge", apple.Nonce);
    Assert.Equal(0, apple.SignInCalls);
  }

  [Fact]
  public async Task MissingGoogleSdkAdapterFailsClosedWithoutUsingAppleOrBroker()
  {
    var apple = new AppleAdapter();
    await Assert.ThrowsAsync<NotSupportedException>(() => new PlatformReAuthProviderAssertion(apple)
        .GetCredentialAsync("google", "server-challenge", TestContext.Current.CancellationToken));
    Assert.Null(apple.Nonce);
    Assert.Equal(0, apple.SignInCalls);
  }

  private sealed class AppleAdapter : IAppleSignInProvider, IReAuthAppleSignInProvider
  {
    public string? Nonce { get; private set; }
    public int SignInCalls { get; private set; }
    public Task<AppleSignInCredential> GetCredentialAsync(CancellationToken cancellationToken = default)
    { SignInCalls++; throw new InvalidOperationException(); }
    public Task<AppleSignInCredential> GetCredentialAsync(string nonce, CancellationToken cancellationToken = default)
    { Nonce = nonce; return Task.FromResult(new AppleSignInCredential("fresh-apple-token", nonce, null)); }
  }
}
