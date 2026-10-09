#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task CreateApiKeyAsync(CancellationToken cancellationToken = default)
  {
    if (IsApiKeySecretOperationBusy) return;
    if (!CanCreateApiKey)
    {
      SetCredentialNotice(Localization.UiMessageKey.NativeCredentialsInvalidSelection);
      return;
    }
    var ownerId = currentUserIdOrSlug;
    var ownerInvalidationGeneration = Volatile.Read(ref apiKeyOwnerInvalidationGeneration);
    if (ownerId is null || Volatile.Read(ref settingsIdentityLoadGeneration) != Volatile.Read(ref settingsLoadGeneration)) return;
    if (!TryBeginApiKeySecretOperation(out var secretOperation)) return;
    try
    {
      var response = await settingsService.CreateApiKeyAsync(
          ApiKeyLabel, ApiKeyType, SelectedApiKeyScopes, ApiKeyLifetimeDays, cancellationToken)
          .ConfigureAwait(true);
      if (!IsCurrentApiKeyOwner(ownerId, ownerInvalidationGeneration) ||
          !IsCurrentApiKeySecretOperation(secretOperation)) return;
      ApiKeyLabel = string.Empty;
      await LoadAsync(cancellationToken).ConfigureAwait(true);
      PublishOrHoldCreatedApiKeySecret(ownerId, response.RawKey, ownerInvalidationGeneration, secretOperation);
    }
    catch (Exception ex)
    {
      if (ex is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized } &&
          IsCurrentApiKeySecretOperation(secretOperation))
        InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      else if (IsCurrentApiKeyOwner(ownerId, ownerInvalidationGeneration) &&
               IsCurrentApiKeySecretOperation(secretOperation)) ErrorMessage = ex.Message;
    }
    finally
    {
      CompleteApiKeySecretOperation(secretOperation);
    }
  }

  public void DismissApiKeySecret()
  {
    if (ApiKeySecret is null) return;
    retainedApiKeyDisclosure = null;
    ApiKeySecret = null;
    NotifyApiKeySecretAdmission();
  }

  private Localization.UiMessageKey? apiKeyRotationNoticeKey;

  public string? ApiKeyRotationNotice => apiKeyRotationNoticeKey is { } key ? localization.Localize(key) : null;

  private readonly Dictionary<string, long> rotatingApiKeyIds = new(StringComparer.Ordinal);
  private long apiKeyRotationSequence;

  public async Task RotateApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(apiKey);
    if (Volatile.Read(ref settingsViewModelDisposed) != 0) return;
    var generation = Volatile.Read(ref settingsLoadGeneration);
    var ownerId = currentUserIdOrSlug;
    if (ownerId is null || Volatile.Read(ref settingsIdentityLoadGeneration) != generation) return;
    if (apiKey.RevokedAt is not null || apiKey.ReplacedByApiKeyId is not null ||
        apiKey.ExpiresAt <= DateTimeOffset.UtcNow || rotatingApiKeyIds.ContainsKey(apiKey.Id) ||
        !TryBeginApiKeySecretOperation(out var secretOperation)) return;
    var rotation = unchecked(++apiKeyRotationSequence);
    rotatingApiKeyIds.Add(apiKey.Id, rotation);
    var ownerInvalidationGeneration = Volatile.Read(ref apiKeyOwnerInvalidationGeneration);
    apiKeyRotationNoticeKey = null;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
    try
    {
      var response = await settingsService.RotateApiKeyAsync(apiKey.Id, cancellationToken).ConfigureAwait(true);
      if (!PublishOrHoldRotatedApiKeySecret(ownerId, response.RawKey,
              ownerInvalidationGeneration, secretOperation)) return;
      apiKeyRotationNoticeKey = Localization.UiMessageKey.NativeApiKeysRotated;
      OnPropertyChanged(nameof(ApiKeyRotationNotice));
      try
      {
        var page = await settingsService.FetchApiKeysAsync(cancellationToken).ConfigureAwait(true);
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug &&
            IsCurrentApiKeySecretOperation(secretOperation)) ReplaceApiKeyPage(page);
      }
      catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
      {
        if (ex is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
        {
          if (IsCurrentApiKeySecretOperation(secretOperation))
            InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
          return;
        }
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug &&
            IsCurrentApiKeySecretOperation(secretOperation))
          ApiKeys = [response.ApiKey, .. ApiKeys.Where(key => key.Id != apiKey.Id && key.Id != response.ApiKey.Id)];
      }
    }
    catch (VouchaApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict)
    {
      if (!IsCurrentSettingsLoad(generation) || ownerId != currentUserIdOrSlug ||
          !IsCurrentApiKeySecretOperation(secretOperation)) return;
      apiKeyRotationNoticeKey = ex.StatusCode == System.Net.HttpStatusCode.NotFound
          ? Localization.UiMessageKey.NativeApiKeysRotationNotFound
          : Localization.UiMessageKey.NativeApiKeysRotationConflict;
      OnPropertyChanged(nameof(ApiKeyRotationNotice));
      try
      {
        var page = await settingsService.FetchApiKeysAsync(cancellationToken).ConfigureAwait(true);
        if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug &&
            IsCurrentApiKeySecretOperation(secretOperation)) ReplaceApiKeyPage(page);
      }
      catch (Exception fetchException) when (!cancellationToken.IsCancellationRequested)
      {
        if (fetchException is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
        {
          if (IsCurrentApiKeySecretOperation(secretOperation))
            InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
        }
      }
    }
    catch (Exception ex)
    {
      if (ex is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
      {
        if (IsCurrentApiKeySecretOperation(secretOperation))
          InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      }
      if (IsCurrentSettingsLoad(generation) && ownerId == currentUserIdOrSlug &&
          IsCurrentApiKeySecretOperation(secretOperation)) ErrorMessage = ex.Message;
    }
    finally
    {
      if (rotatingApiKeyIds.TryGetValue(apiKey.Id, out var active) && active == rotation)
        rotatingApiKeyIds.Remove(apiKey.Id);
      CompleteApiKeySecretOperation(secretOperation);
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
