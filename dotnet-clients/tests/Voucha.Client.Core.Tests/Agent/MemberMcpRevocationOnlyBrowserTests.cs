using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpRevocationOnlyBrowserTests
{
  [Fact]
  public async Task OpeningABrowserIsRejectedBecauseBrowserAuthorizationIsRetired()
  {
    var browser = new MemberMcpRevocationOnlyBrowser();

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        browser.OpenAsync(new Uri("https://example.test/authorize"), TestContext.Current.CancellationToken));
  }

  [Fact]
  public void FactoriesWithoutLegacyGrantsDefaultToNoLegacyRevocationFactory()
  {
    IMemberMcpSessionConnectionFactory factory = new FakeLegacyHost();

    Assert.Null(factory.LegacyGrantRevocationFactory);
  }

  private sealed class FakeLegacyHost : IMemberMcpSessionConnectionFactory
  {
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }
}
