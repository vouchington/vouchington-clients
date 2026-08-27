#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task CreateApiKeyAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var response = await settingsService.CreateApiKeyAsync(ApiKeyLabel, ApiKeyType, cancellationToken)
          .ConfigureAwait(true);
      ApiKeyLabel = string.Empty;
      await LoadAsync(cancellationToken).ConfigureAwait(true);
      ApiKeySecret = response.RawKey;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
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
