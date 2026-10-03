#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task CreateApiKeyAsync(CancellationToken cancellationToken = default)
  {
    if (!CanCreateApiKey)
    {
      SetCredentialNotice(Localization.UiMessageKey.NativeCredentialsInvalidSelection);
      return;
    }
    isCreatingApiKey = true;
    OnPropertyChanged(nameof(CanCreateApiKey));
    ApiKeySecret = null;
    try
    {
      var response = await settingsService.CreateApiKeyAsync(ApiKeyLabel, ApiKeyType, SelectedApiKeyScopes, cancellationToken)
          .ConfigureAwait(true);
      ApiKeyLabel = string.Empty;
      await LoadAsync(cancellationToken).ConfigureAwait(true);
      ApiKeySecret = response.RawKey;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      isCreatingApiKey = false;
      OnPropertyChanged(nameof(CanCreateApiKey));
    }
  }

  public async Task RevokeApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(apiKey);
    try
    {
      await settingsService.DeleteApiKeyAsync(apiKey.Id, cancellationToken).ConfigureAwait(true);
      RemoveApiKeyFromPage(apiKey.Id);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }
}
#pragma warning restore CA1031
