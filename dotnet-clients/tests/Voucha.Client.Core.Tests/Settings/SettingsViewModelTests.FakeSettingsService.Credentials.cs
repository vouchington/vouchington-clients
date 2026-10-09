using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  private partial class FakeSettingsService
  {
    public IReadOnlyList<string> Roles { get; set; } = [];
    public Exception? CatalogFailure { get; set; }
    public ScopeCatalogResponse Catalog { get; set; } = SettingsCredentialTestFixtures.Catalog;
    public Task<ScopeCatalogResponse>? CatalogTask { get; set; }
    public Task<OAuthGrantListResponse> Grants { get; set; } = Task.FromResult(SettingsCredentialTestFixtures.EmptyGrants);
    public Func<string?, Task<OAuthGrantListResponse>>? FetchGrantsPage { get; set; }
    public Func<Task>? RevokeGrant { get; set; }
    public string? LastGrantCursor { get; private set; }
    public string? LastRevokedGrant { get; private set; }
    public IReadOnlyList<string>? LastCreatedApiKeyPermissions { get; set; }
    public int? LastCreatedApiKeyLifetimeDays { get; private set; }
    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
        string label, string type, IReadOnlyList<string> permissions, int? lifetimeDays,
        CancellationToken cancellationToken = default)
    {
      LastCreatedApiKeyLifetimeDays = lifetimeDays;
      return CreateApiKeyAsync(label, type, permissions, cancellationToken);
    }
    public Task<ScopeCatalogResponse> FetchScopeCatalogAsync(CancellationToken cancellationToken = default) =>
        CatalogFailure is { } error ? Task.FromException<ScopeCatalogResponse>(error) : CatalogTask ?? Task.FromResult(Catalog);
    public Task<OAuthGrantListResponse> FetchOAuthGrantsAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      LastGrantCursor = after;
      return FetchGrantsPage?.Invoke(after) ?? Grants;
    }
    public Task RevokeOAuthGrantAsync(string id, CancellationToken cancellationToken = default)
    {
      LastRevokedGrant = id;
      return RevokeGrant?.Invoke() ?? Task.CompletedTask;
    }
  }
}
