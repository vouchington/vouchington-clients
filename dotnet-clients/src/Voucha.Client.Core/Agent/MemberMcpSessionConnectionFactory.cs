namespace Voucha.Client.Core.Agent;

public sealed class MemberMcpSessionConnectionFactory(
    Uri siteOrigin, IMemberMcpAccountTokenStore store, IMemberMcpSystemBrowser browser)
    : IMemberMcpSessionConnectionFactory
{
  public async Task<IMemberMcpSessionConnection> CreateAsync(
      string accountId, CancellationToken cancellationToken)
  {
    using var discovery = new MemberMcpOAuthDiscovery(siteOrigin);
    var metadata = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
    return new MemberMcpSessionConnection(siteOrigin, accountId, metadata, store, browser);
  }
}
