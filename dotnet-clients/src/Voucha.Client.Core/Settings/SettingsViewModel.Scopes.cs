using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private readonly ApiKeyScopeSelection scopeSelection = new([]);
  private IReadOnlyList<ScopeCatalogEntry> scopeCatalog = [];
  private bool isCreatingApiKey;
  private UiMessageKey? credentialNoticeKey;

  public bool CanCreateApiKey => ApiKeyType is "rss" or "mcp" && loadedUser is not null && !isCreatingApiKey && ApiKeyLabel.Trim().Length > 0 &&
      SelectedApiKeyScopes.Count > 0 && (ApiKeyType != "rss" || SelectedApiKeyScopes.Count == 1) &&
      (!IsApiKeyAdministrator || apiKeyLifetime is "30" or "90");
  public string? CredentialNotice => credentialNoticeKey is { } key ? localization.Localize(key) : null;
  public IReadOnlyList<string> SelectedApiKeyScopes => scopeSelection.SelectedScopes;
  public IReadOnlyList<SettingsScopeRow> ApiKeyScopes => scopeSelection.Scopes
      .Select(scope => new SettingsScopeRow(scope, SelectedApiKeyScopes.Contains(scope.Scope, StringComparer.Ordinal), localization)).ToArray();
  public void SetApiKeyScopeSelected(string scope, bool selected)
  {
    try
    {
      scopeSelection.SetSelected(scope, selected, ApiKeyType == "rss" ? 1 : null);
      SetCredentialNotice(null);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
      SetCredentialNotice(UiMessageKey.NativeCredentialsInvalidSelection);
    }
    NotifyScopeSelection();
  }

  private void RebuildScopeSelection()
  {
    var audience = ApiKeyType == "rss" ? "api" : "user";
    scopeSelection.Replace(scopeCatalog.Where(scope =>
        scope.Surfaces.Contains("api-key", StringComparer.Ordinal) && scope.Audience == audience &&
        !scope.Scope.StartsWith("mcp.admin:", StringComparison.OrdinalIgnoreCase) &&
        scope.Action is "read" or "write").ToArray());
    NotifyScopeSelection();
  }

  private void NotifyScopeSelection()
  {
    OnPropertyChanged(nameof(ApiKeyScopes));
    OnPropertyChanged(nameof(SelectedApiKeyScopes));
    OnPropertyChanged(nameof(CanCreateApiKey));
  }

  private void ResetCredentialAuthorization()
  {
    loadedUser = null;
    scopeCatalog = [];
    RebuildScopeSelection();
  }

  private async Task LoadCredentialsAsync(int generation, CancellationToken cancellationToken)
  {
    var catalogTask = LoadScopeCatalogAsync(generation, cancellationToken);
    var grantsTask = LoadOAuthGrantsAsync(cancellationToken);
    await Task.WhenAll(catalogTask, grantsTask).ConfigureAwait(true);
  }

  private async Task LoadScopeCatalogAsync(int generation, CancellationToken cancellationToken)
  {
    try
    {
      var response = await settingsService.FetchScopeCatalogAsync(cancellationToken).ConfigureAwait(true);
      if (!IsCurrentSettingsLoad(generation)) return;
      if (response.Scopes.Any(scope => !SettingsScopeRow.IsSupportedDescriptionKey(scope.DescriptionKey)))
        throw new InvalidOperationException("Unknown scope description.");
      scopeCatalog = response.Scopes;
      RebuildScopeSelection();
      SetCredentialNotice(null);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or ArgumentException or OperationCanceledException)
    {
      if (!IsCurrentSettingsLoad(generation)) return;
      scopeCatalog = [];
      RebuildScopeSelection();
      SetCredentialNotice(UiMessageKey.NativeCredentialsCatalogLoadFailed);
    }
  }

  private void SetCredentialNotice(UiMessageKey? key) =>
      SetProperty(ref credentialNoticeKey, key, nameof(CredentialNotice));
}
