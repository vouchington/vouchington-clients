using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private IReadOnlyList<LocalLLMEndpointProfile> localLLMEndpoints = [];
  private Guid? localLLMSelectedEndpointId;
  private string? localLLMSelectedProviderId;
  private bool localLLMEditingExistingEndpoint;
  private bool localLLMApiKeyLoading;

  public IReadOnlyList<LocalLLMEndpointProfile> LocalLLMEndpoints
  {
    get => localLLMEndpoints;
    private set
    {
      if (SetProperty(ref localLLMEndpoints, value))
      {
        OnPropertyChanged(nameof(LocalizedLocalLLMEndpoints));
      }
    }
  }

  public Guid? LocalLLMSelectedEndpointId
  {
    get => localLLMSelectedEndpointId;
    private set
    {
      if (SetProperty(ref localLLMSelectedEndpointId, value))
      {
        OnPropertyChanged(nameof(LocalizedLocalLLMEndpoints));
      }
    }
  }

  public string? LocalLLMSelectedProviderId
  {
    get => localLLMSelectedProviderId;
    private set
    {
      if (SetProperty(ref localLLMSelectedProviderId, value))
      {
        OnPropertyChanged(nameof(LocalizedLocalLLMEndpoints));
      }
    }
  }

  private void RefreshLocalLLMEndpoints(LocalLLMConfiguration configuration)
  {
    LocalLLMEndpoints = configuration.Profiles;
    LocalLLMSelectedEndpointId = configuration.SelectedEndpointId;
    LocalLLMSelectedProviderId = configuration.SelectedProviderId;
  }

  private async Task ApplyProfileDraftAsync(LocalLLMEndpointProfile profile, CancellationToken cancellationToken)
  {
    ApplyProfile(profile);
    localLLMEditingExistingEndpoint = true;
    var targetProfileId = localLLMProfileId;
    var requestedOrigin = profile.Origin;
    localLLMApiKey = string.Empty;
    localLLMApiKeyDirty = false;
    localLLMApiKeyLoading = true;
    OnPropertyChanged(nameof(LocalLLMApiKey));
    try
    {
      var apiKey = profile.RequiresCredentialReplacement
          ? string.Empty
          : await localLLMSecretStore.ReadApiKeyAsync(targetProfileId, cancellationToken).ConfigureAwait(true) ?? string.Empty;
      var live = localLLMConfigurationStore.Load().Profiles.SingleOrDefault(item => item.Id == targetProfileId);
      if (localLLMProfileId != targetProfileId || localLLMApiKeyDirty || live?.Origin != requestedOrigin) return;
      localLLMApiKey = apiKey;
      localLLMApiKeyDirty = false;
      OnPropertyChanged(nameof(LocalLLMApiKey));
    }
    finally
    {
      if (localLLMProfileId == targetProfileId) localLLMApiKeyLoading = false;
    }
  }

  public void ResetLocalLLMEndpointDraft()
  {
    localLLMProfileId = Guid.NewGuid();
    localLLMEditingExistingEndpoint = false;
    localLLMApiKeyLoading = false;
    LocalLLMDisplayName = string.Empty;
    LocalLLMEnabled = true;
    LocalLLMEndpoint = string.Empty;
    LocalLLMModelsText = string.Empty;
    LocalLLMSelectedModel = string.Empty;
    LocalLLMApiKey = string.Empty;
    localLLMApiKeyDirty = false;
    OnPropertyChanged(nameof(LocalLLMDraftEndpointId));
  }

  public async Task SelectLocalLLMEndpointDraftAsync(LocalLLMEndpointProfile profile, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(profile);
    await ApplyProfileDraftAsync(profile, cancellationToken).ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations surface failures in view state.")]
  public Task ActivateLocalLLMEndpointAsync(LocalLLMEndpointProfile profile, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(profile);
    try
    {
      localLLMConfigurationStore.Update(configuration =>
      {
        var stored = configuration.Profiles.SingleOrDefault(item => item.Id == profile.Id);
        if (stored is null)
        {
          throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetCsharpLocalModelEndpointMissing));
        }
        if (!stored.IsEnabled)
        {
          throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetCsharpLocalModelEndpointDisabled));
        }
        ValidateProfile(stored);
        return configuration with
        {
          SelectedEndpointId = profile.Id,
          SelectedProviderId = LocalChatProviderIds.OpenAICompatible(profile.Id),
        };
      });
      RefreshLocalLLMEndpoints(localLLMConfigurationStore.Load());
    }
    catch (Exception ex) { LocalLLMStatusMessage = ex.Message; }
    return Task.CompletedTask;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations surface failures in view state.")]
  public async Task DeleteLocalLLMEndpointAsync(LocalLLMEndpointProfile profile, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(profile);
    try
    {
      var providerId = LocalChatProviderIds.OpenAICompatible(profile.Id);
      await localLLMConfigurationStore.TransactionAsync(async transaction =>
      {
        var remaining = transaction.Configuration.Profiles.Where(item => item.Id != profile.Id).ToArray();
        var wasSelected = transaction.Configuration.SelectedEndpointId == profile.Id;
        var providerMatched = transaction.Configuration.SelectedProviderId == providerId;
        transaction.Save(SafeBeforeSecretReplacement(transaction.Configuration, profile.Id));
        await localLLMSecretStore.ClearApiKeyAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        transaction.Save(transaction.Configuration with
        {
          Endpoints = remaining,
          SelectedEndpointId = wasSelected ? remaining.FirstOrDefault()?.Id : transaction.Configuration.SelectedEndpointId,
          SelectedProviderId = providerMatched ? null : transaction.Configuration.SelectedProviderId,
        });
      }, cancellationToken).ConfigureAwait(true);
      RefreshLocalLLMEndpoints(localLLMConfigurationStore.Load());
      if (localLLMProfileId == profile.Id) ResetLocalLLMEndpointDraft();
      LocalLLMStatusMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpLocalModelDeleted);
    }
    catch (Exception ex)
    {
      RefreshLocalLLMEndpoints(localLLMConfigurationStore.Load());
      LocalLLMStatusMessage = ex.Message;
    }
  }
}
