using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private readonly ApiKeyScopeSelection scopeSelection = new([]);
  private IReadOnlyList<ScopeCatalogEntry> scopeCatalog = [];
  private string apiKeyAudience = "user";
  private bool isCreatingApiKey;
  private UiMessageKey? credentialNoticeKey;

  public bool CanSelectAdminApiKeyScopes => loadedUser?.Roles?.Contains("administrator", StringComparer.Ordinal) == true;
  public bool ShowsApiKeyAudience => CanSelectAdminApiKeyScopes && ApiKeyType == "mcp";
  public bool CanCreateApiKey => ApiKeyType is "rss" or "mcp" && !IsLoading && !isCreatingApiKey && ApiKeyLabel.Trim().Length > 0 &&
      SelectedApiKeyScopes.Count > 0 && (ApiKeyType != "rss" || SelectedApiKeyScopes.Count == 1);
  public string? CredentialNotice => credentialNoticeKey is { } key ? localization.Localize(key) : null;
  public IReadOnlyList<string> SelectedApiKeyScopes => scopeSelection.SelectedScopes;
  public IReadOnlyList<SettingsScopeRow> ApiKeyScopes => scopeSelection.Scopes
      .Select(scope => new SettingsScopeRow(scope, SelectedApiKeyScopes.Contains(scope.Scope, StringComparer.Ordinal), localization)).ToArray();
  public IReadOnlyList<UiProtocolOption> ApiKeyAudienceOptions =>
      (CanSelectAdminApiKeyScopes
          ? new[] { new UiProtocolOptionDefinition("user", UiMessageKey.NativeCredentialsUserAudience), new UiProtocolOptionDefinition("admin", UiMessageKey.NativeCredentialsAdminAudience) }
          : new[] { new UiProtocolOptionDefinition("user", UiMessageKey.NativeCredentialsUserAudience) })
      .Select(option => UiProtocolOption.From(option, localization)).ToArray();
  public UiProtocolOption SelectedApiKeyAudienceOption
  {
    get => SelectedOption(ApiKeyAudienceOptions, apiKeyAudience);
    set
    {
      if (value is null) return;
      var audience = value.ProtocolValue == "admin" && CanSelectAdminApiKeyScopes ? "admin" : "user";
      if (audience == apiKeyAudience) return;
      apiKeyAudience = audience;
      RebuildScopeSelection();
      OnPropertyChanged(nameof(SelectedApiKeyAudienceOption));
    }
  }

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
    var audience = ApiKeyType == "rss" ? "api" : apiKeyAudience;
    scopeSelection.Replace(scopeCatalog.Where(scope =>
        scope.Surfaces.Contains("api-key", StringComparer.Ordinal) && scope.Audience == audience &&
        scope.Action is "read" or "write").ToArray());
    OnPropertyChanged(nameof(ShowsApiKeyAudience));
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
    apiKeyAudience = "user";
    RebuildScopeSelection();
    OnPropertyChanged(nameof(CanSelectAdminApiKeyScopes));
    OnPropertyChanged(nameof(ApiKeyAudienceOptions));
    OnPropertyChanged(nameof(SelectedApiKeyAudienceOption));
  }

  private async Task LoadCredentialsAsync(CancellationToken cancellationToken)
  {
    try
    {
      var response = await settingsService.FetchScopeCatalogAsync(cancellationToken).ConfigureAwait(true);
      if (response.Scopes.Any(scope => !SettingsScopeRow.IsSupportedDescriptionKey(scope.DescriptionKey)))
        throw new InvalidOperationException("Unknown scope description.");
      scopeCatalog = response.Scopes;
      if (!CanSelectAdminApiKeyScopes) apiKeyAudience = "user";
      RebuildScopeSelection();
      OnPropertyChanged(nameof(CanSelectAdminApiKeyScopes));
      OnPropertyChanged(nameof(ApiKeyAudienceOptions));
      OnPropertyChanged(nameof(SelectedApiKeyAudienceOption));
      SetCredentialNotice(null);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or ArgumentException or OperationCanceledException)
    {
      scopeCatalog = [];
      RebuildScopeSelection();
      SetCredentialNotice(UiMessageKey.NativeCredentialsCatalogLoadFailed);
    }
    await LoadOAuthGrantsAsync(cancellationToken).ConfigureAwait(true);
  }

  private void SetCredentialNotice(UiMessageKey? key) =>
      SetProperty(ref credentialNoticeKey, key, nameof(CredentialNotice));
}
