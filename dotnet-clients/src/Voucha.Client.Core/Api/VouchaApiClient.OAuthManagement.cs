namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ScopeCatalogResponse> FetchScopeCatalogAsync(CancellationToken cancellationToken = default) =>
      SendAsync<ScopeCatalogResponse>(VouchaApiEndpoints.ScopeCatalog(), cancellationToken);

  public Task<OAuthGrantListResponse> FetchOAuthGrantsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<OAuthGrantListResponse>(VouchaApiEndpoints.OAuthGrants(after, limit), cancellationToken);

  public Task RevokeOAuthGrantAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.RevokeOAuthGrant(id), cancellationToken);
}
