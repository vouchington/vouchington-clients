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
      var response = await settingsService.CreateApiKeyAsync(
          ApiKeyLabel, ApiKeyType, SelectedApiKeyScopes, ApiKeyLifetimeDays, cancellationToken)
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

  public void DismissApiKeySecret() => ApiKeySecret = null;

  private Localization.UiMessageKey? apiKeyRotationNoticeKey;

  public string? ApiKeyRotationNotice => apiKeyRotationNoticeKey is { } key ? localization.Localize(key) : null;

  private readonly HashSet<string> rotatingApiKeyIds = new(StringComparer.Ordinal);

  public async Task RotateApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(apiKey);
    if (apiKey.RevokedAt is not null || apiKey.ReplacedByApiKeyId is not null ||
        apiKey.ExpiresAt <= DateTimeOffset.UtcNow || !rotatingApiKeyIds.Add(apiKey.Id)) return;
    var generation = Volatile.Read(ref settingsLoadGeneration);
    var ownerId = currentUserIdOrSlug;
    ApiKeySecret = null;
    apiKeyRotationNoticeKey = null;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
    try
    {
      var response = await settingsService.RotateApiKeyAsync(apiKey.Id, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentSettingsLoad(generation) || ownerId != currentUserIdOrSlug) return;
      ApiKeySecret = response.RawKey;
      apiKeyRotationNoticeKey = Localization.UiMessageKey.NativeApiKeysRotated;
      OnPropertyChanged(nameof(ApiKeyRotationNotice));
      try
      {
        var page = await settingsService.FetchApiKeysAsync(cancellationToken).ConfigureAwait(true);
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug) ReplaceApiKeyPage(page);
      }
      catch (Exception) when (!cancellationToken.IsCancellationRequested)
      {
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug)
          ApiKeys = [response.ApiKey, .. ApiKeys.Where(key => key.Id != apiKey.Id && key.Id != response.ApiKey.Id)];
      }
    }
    catch (VouchaApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict)
    {
      if (!IsCurrentSettingsLoad(generation) || ownerId != currentUserIdOrSlug) return;
      apiKeyRotationNoticeKey = ex.StatusCode == System.Net.HttpStatusCode.NotFound
          ? Localization.UiMessageKey.NativeApiKeysRotationNotFound
          : Localization.UiMessageKey.NativeApiKeysRotationConflict;
      OnPropertyChanged(nameof(ApiKeyRotationNotice));
      try
      {
        var page = await settingsService.FetchApiKeysAsync(cancellationToken).ConfigureAwait(true);
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug) ReplaceApiKeyPage(page);
      }
      catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }
    catch (Exception ex)
    {
      if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug) ErrorMessage = ex.Message;
    }
    finally
    {
      if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug) rotatingApiKeyIds.Remove(apiKey.Id);
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
