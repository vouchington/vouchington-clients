using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public partial interface ISettingsService
{
  Task<ScopeCatalogResponse> FetchScopeCatalogAsync(CancellationToken cancellationToken = default);

  Task<OAuthGrantListResponse> FetchOAuthGrantsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task RevokeOAuthGrantAsync(string id, CancellationToken cancellationToken = default);
}
