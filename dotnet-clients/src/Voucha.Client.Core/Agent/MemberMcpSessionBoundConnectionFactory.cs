using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Agent;

/// <summary>Creates session-bound MCP connections; no OAuth discovery or browser is involved.</summary>
public sealed class MemberMcpSessionBoundConnectionFactory(
    Uri siteOrigin, VouchaApiClient api, ISessionStore sessionStore,
    IMemberMcpSessionConnectionFactory legacyGrantRevocationFactory,
    Func<HttpMessageHandler>? createHandler = null, string clientApp = "windows")
    : IMemberMcpSessionConnectionFactory
{
  /// <summary>
  /// Revokes grants a retired browser authorization left behind; never opens a browser. Required so a
  /// composition cannot silently skip remote revocation of those grants.
  /// </summary>
  public IMemberMcpSessionConnectionFactory LegacyGrantRevocationFactory { get; } =
      legacyGrantRevocationFactory ?? throw new ArgumentNullException(nameof(legacyGrantRevocationFactory));

  public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var tokens = new MemberMcpSessionTokenSource(
        api, new Uri(siteOrigin, "/api/v1/mcp"), clientApp,
        stillCurrent: () => sessionStore.Current.Identity?.Id == accountId);
    var client = new MemberMcpClient(siteOrigin, createHandler?.Invoke());
    return Task.FromResult<IMemberMcpSessionConnection>(new MemberMcpSessionBoundConnection(client, tokens));
  }
}
