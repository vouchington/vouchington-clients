namespace Voucha.Client.Core.Agent;

public sealed class MemberMcpSessionConnectionFactory(
    Uri siteOrigin, IMemberMcpAccountTokenStore store, IMemberMcpSystemBrowser browser,
    Func<HttpMessageHandler>? createHandler = null)
    : IMemberMcpSessionConnectionFactory
{
  public async Task<IMemberMcpSessionConnection> CreateAsync(
      string accountId, CancellationToken cancellationToken)
  {
    using var discovery = new MemberMcpOAuthDiscovery(siteOrigin, createHandler?.Invoke());
    var metadata = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
    return new MemberMcpSessionConnection(siteOrigin, accountId, metadata, store, browser, createHandler);
  }
}
