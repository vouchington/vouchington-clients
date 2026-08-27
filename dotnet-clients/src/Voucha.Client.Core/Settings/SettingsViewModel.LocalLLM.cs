using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private Guid localLLMProfileId = Guid.NewGuid();
  private string? localLLMStatusMessage; private bool localLLMEnabled, localLLMApiKeyDirty;
  private string localLLMDisplayName = string.Empty, localLLMEndpoint = string.Empty, localLLMModelsText = string.Empty, localLLMSelectedModel = string.Empty, localLLMApiKey = string.Empty;
  public bool LocalLLMSettingsAvailable => localLLMFeaturePolicy.IsEnabled;
  public string? LocalLLMStatusMessage { get => localLLMStatusMessage; private set => SetProperty(ref localLLMStatusMessage, value); }
  public bool LocalLLMEnabled { get => localLLMEnabled; set => SetProperty(ref localLLMEnabled, value); }
  public string LocalLLMDisplayName { get => localLLMDisplayName; set => SetProperty(ref localLLMDisplayName, value ?? string.Empty); }
  public string LocalLLMEndpoint { get => localLLMEndpoint; set => SetProperty(ref localLLMEndpoint, value ?? string.Empty); }
  public string LocalLLMModelsText { get => localLLMModelsText; set => SetProperty(ref localLLMModelsText, value ?? string.Empty); }
  public string LocalLLMSelectedModel { get => localLLMSelectedModel; set => SetProperty(ref localLLMSelectedModel, value ?? string.Empty); }
  public string LocalLLMApiKey { get => localLLMApiKey; set { localLLMApiKeyDirty = true; SetProperty(ref localLLMApiKey, value ?? string.Empty); } }
  public Guid LocalLLMDraftEndpointId => localLLMProfileId;

  public async Task LoadLocalLLMSettingsAsync(CancellationToken cancellationToken = default)
  {
    var configuration = localLLMConfigurationStore.Load();
    RefreshLocalLLMEndpoints(configuration);
    var profile = configuration.SelectedEndpoint ??
        (configuration.Profiles.Count > 0 ? configuration.Profiles[0] : null);
    if (profile is null) ResetLocalLLMEndpointDraft();
    else await ApplyProfileDraftAsync(profile, cancellationToken).ConfigureAwait(true);
    LocalLLMStatusMessage = LocalLLMSettingsAvailable ? localization.Localize(UiMessageKey.NativeDotnetSettingsLocalModelSavedOnDevice) : localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalModelsOffPlatform);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations surface failures in view state.")]
  public async Task SaveLocalLLMSettingsAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      if (localLLMApiKeyLoading) return;
      var profile = BuildProfile(); ValidateProfile(profile);
      var submittedProfileId = profile.Id;
      var submittedSelectedModel = LocalLLMSelectedModel;
      var editingExistingEndpoint = localLLMEditingExistingEndpoint;
      var apiKey = LocalLLMApiKey;
      var apiKeyIsReplacement = localLLMApiKeyDirty;
      var originChanged = false;
      var stageCredentialSave = false;
      var requiresSecretCleanup = false;
      var preservesCredentialReplacement = false;
      var originTombstonePersisted = false;
      var clearedLoadedApiKey = false;
      try
      {
        await localLLMConfigurationStore.TransactionAsync(async transaction =>
        {
          var previous = transaction.Configuration.Profiles.SingleOrDefault(item => item.Id == profile.Id);
          if (previous is null && editingExistingEndpoint)
          {
            throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetCsharpLocalModelEndpointMissing));
          }
          preservesCredentialReplacement = previous?.RequiresCredentialReplacement == true && !profile.IsEnabled;
          var desiredProfile = previous?.RequiresCredentialReplacement == true && !profile.IsEnabled
              ? profile with { RequiresCredentialReplacement = true }
              : profile;
          var desired = WithProfile(transaction.Configuration, desiredProfile);
          originChanged = previous is not null && previous.Origin != profile.Origin;
          requiresSecretCleanup = previous?.RequiresCredentialReplacement == true && profile.IsEnabled;
          stageCredentialSave = profile.IsEnabled && (apiKeyIsReplacement || !string.IsNullOrWhiteSpace(apiKey)) && (previous is null || !previous.IsEnabled);
          if (!originChanged && !stageCredentialSave && !requiresSecretCleanup)
          {
            transaction.Save(desired);
            if (!preservesCredentialReplacement)
            {
              await localLLMSecretStore.SaveApiKeyAsync(profile.Id, apiKey, cancellationToken).ConfigureAwait(false);
            }
            return;
          }
          var staged = (requiresSecretCleanup || originChanged)
              ? WithProfile(transaction.Configuration, profile with { RequiresCredentialReplacement = true })
              : desired;
          transaction.Save(SafeBeforeSecretReplacement(staged, profile.Id));
          if (originChanged)
          {
            originTombstonePersisted = !apiKeyIsReplacement;
            await localLLMSecretStore.ClearApiKeyAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            if (apiKeyIsReplacement) await localLLMSecretStore.SaveApiKeyAsync(profile.Id, apiKey, cancellationToken).ConfigureAwait(false);
            else clearedLoadedApiKey = true;
          }
          else if (requiresSecretCleanup)
          {
            await localLLMSecretStore.ClearApiKeyAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            if (apiKeyIsReplacement) await localLLMSecretStore.SaveApiKeyAsync(profile.Id, apiKey, cancellationToken).ConfigureAwait(false);
            else clearedLoadedApiKey = true;
          }
          else await localLLMSecretStore.SaveApiKeyAsync(profile.Id, apiKey, cancellationToken).ConfigureAwait(false);
          transaction.Save(desired);
        }, cancellationToken).ConfigureAwait(true);
      }
      finally
      {
        if ((clearedLoadedApiKey || originTombstonePersisted) && localLLMProfileId == submittedProfileId)
        {
          localLLMApiKey = string.Empty;
          OnPropertyChanged(nameof(LocalLLMApiKey));
        }
      }
      if (localLLMProfileId == submittedProfileId)
      {
        localLLMApiKeyDirty = false;
        localLLMEditingExistingEndpoint = true;
        if (LocalLLMSelectedModel == submittedSelectedModel)
        {
          LocalLLMSelectedModel = profile.SelectedModelName;
        }
      }
      RefreshLocalLLMEndpoints(localLLMConfigurationStore.Load());
      LocalLLMStatusMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpLocalModelSaved);
    }
    catch (Exception ex)
    {
      RefreshLocalLLMEndpoints(localLLMConfigurationStore.Load());
      LocalLLMStatusMessage = ex.Message;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations surface failures in view state.")]
  public async Task TestLocalLLMSettingsAsync(CancellationToken cancellationToken = default)
  {
    try { var profile = BuildProfile(); ValidateProfile(profile); var response = await localLLMResponsesClient.GenerateAssistantContentAsync("Reply with exactly: Voucha local model ready.", [], profile, LocalLLMApiKey, cancellationToken).ConfigureAwait(true); LocalLLMStatusMessage = string.IsNullOrWhiteSpace(response) ? localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse) : localization.Localize(UiMessageKey.NativeDotnetSettingsLocalModelTestSucceeded); }
    catch (Exception ex) { LocalLLMStatusMessage = ex.Message; }
  }

  private LocalLLMEndpointProfile BuildProfile()
  {
    var models = LocalLLMModelsText.Split(['\n', '\r', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
    var selected = string.IsNullOrWhiteSpace(LocalLLMSelectedModel) ? models.FirstOrDefault() ?? string.Empty : LocalLLMSelectedModel.Trim();
    return new(localLLMProfileId, LocalLLMDisplayName.Trim(), LocalLLMEnabled, LocalLLMEndpoint.Trim(), models, selected);
  }
  private static LocalLLMConfiguration WithProfile(LocalLLMConfiguration existing, LocalLLMEndpointProfile profile)
  {
    var providerId = LocalChatProviderIds.OpenAICompatible(profile.Id);
    var selectedEndpoint = existing.SelectedEndpointId;
    var selectedProvider = existing.SelectedProviderId;
    var selectsThisEndpoint = LocalChatProviderIds.RefersTo(selectedProvider, profile.Id);
    if (profile.IsEnabled && !HasValidSelection(existing)) { selectedEndpoint = profile.Id; selectedProvider = providerId; }
    else if (!profile.IsEnabled && selectedEndpoint == profile.Id) { selectedEndpoint = null; if (selectsThisEndpoint) selectedProvider = null; }
    var endpoints = existing.Profiles.Any(item => item.Id == profile.Id)
        ? existing.Profiles.Select(item => item.Id == profile.Id ? profile : item).ToArray()
        : existing.Profiles.Append(profile).ToArray();
    return existing with
    {
      Endpoints = endpoints,
      SelectedEndpointId = selectedEndpoint,
      SelectedProviderId = selectedProvider,
    };
  }
  private static LocalLLMConfiguration SafeBeforeSecretReplacement(LocalLLMConfiguration configuration, Guid profileId)
  {
    var selected = LocalChatProviderIds.RefersTo(configuration.SelectedProviderId, profileId);
    return configuration with
    {
      Endpoints = configuration.Profiles.Select(item => item.Id == profileId ? item with { IsEnabled = false } : item).ToArray(),
      SelectedEndpointId = selected ? null : configuration.SelectedEndpointId,
      SelectedProviderId = selected ? null : configuration.SelectedProviderId,
    };
  }
  private static bool HasValidSelection(LocalLLMConfiguration configuration)
  {
    if (configuration.SelectedProviderId is null) return true;
    if (configuration.SelectedProviderId == LocalChatProviderIds.WindowsSystemLanguageModel) return true;
    return LocalChatProviderIds.TryGetEndpointId(configuration.SelectedProviderId) is { } profileId &&
        configuration.Profiles.Any(item => item.Id == profileId && item.IsEnabled);
  }
  private void ApplyProfile(LocalLLMEndpointProfile profile) { localLLMProfileId = profile.Id; LocalLLMDisplayName = profile.DisplayName; LocalLLMEnabled = profile.IsEnabled; LocalLLMEndpoint = profile.Endpoint; LocalLLMModelsText = string.Join(Environment.NewLine, profile.NormalizedModelNames); LocalLLMSelectedModel = profile.SelectedModelName; }
  private void ValidateProfile(LocalLLMEndpointProfile profile) { if (!profile.IsEnabled) return; if (profile.ResponsesUri is null) throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetChatConversationEnterLocalResponsesEndpoint)); if (profile.SelectedModel is null) throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetChatConversationChooseLocalModel)); }
}
