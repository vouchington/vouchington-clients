using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class ApiSettingsService
{
  public Task<ScopeCatalogResponse> FetchScopeCatalogAsync(CancellationToken cancellationToken = default) =>
      client.FetchScopeCatalogAsync(cancellationToken);

  public Task<OAuthGrantListResponse> FetchOAuthGrantsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchOAuthGrantsAsync(after, limit, cancellationToken);

  public Task RevokeOAuthGrantAsync(string id, CancellationToken cancellationToken = default) =>
      client.RevokeOAuthGrantAsync(id, cancellationToken);
}
