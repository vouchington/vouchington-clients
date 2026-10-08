using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  private sealed partial class RecordingSettingsService
  {
    public IReadOnlyList<string>? LastCreatedApiKeyPermissions { get; private set; }
    public Task<ScopeCatalogResponse> FetchScopeCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SettingsCredentialTestFixtures.Catalog);
    public Task<OAuthGrantListResponse> FetchOAuthGrantsAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
        Task.FromResult(SettingsCredentialTestFixtures.EmptyGrants);
    public Task RevokeOAuthGrantAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
