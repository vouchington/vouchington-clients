namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private int settingsIdentityLoadGeneration;
  private int apiKeyOwnerInvalidationGeneration;
  private int settingsViewModelDisposed;
  private string? pendingRotatedApiKeyOwnerId;
  private string? pendingRotatedApiKeySecret;

  private void ConfirmApiKeyOwnerIdentity(int generation, string ownerId)
  {
    if (currentUserIdOrSlug is { } previousOwnerId && previousOwnerId != ownerId)
      InvalidateApiKeyOwnerIdentity();
    currentUserIdOrSlug = ownerId;
    Volatile.Write(ref settingsIdentityLoadGeneration, generation);
    if (pendingRotatedApiKeySecret is not { } secret) return;
    var pendingOwnerId = pendingRotatedApiKeyOwnerId;
    ClearPendingApiKeyRotationSecret();
    if (pendingOwnerId != ownerId) return;
    ApiKeySecret = secret;
    apiKeyRotationNoticeKey = Localization.UiMessageKey.NativeApiKeysRotated;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
  }

  private void BeginApiKeyOwnerIdentityLoad() => Volatile.Write(ref settingsIdentityLoadGeneration, 0);

  private void InvalidateApiKeyOwnerIdentity()
  {
    Interlocked.Increment(ref apiKeyOwnerInvalidationGeneration);
    currentUserIdOrSlug = null;
    Volatile.Write(ref settingsIdentityLoadGeneration, 0);
    ClearPendingApiKeyRotationSecret();
    ApiKeySecret = null;
  }

  private void InvalidateApiKeyOwnerIdentityIfCurrent(int expectedGeneration)
  {
    if (expectedGeneration == Volatile.Read(ref apiKeyOwnerInvalidationGeneration))
      InvalidateApiKeyOwnerIdentity();
  }

  private void ClearPendingApiKeyRotationSecret()
  {
    pendingRotatedApiKeyOwnerId = null;
    pendingRotatedApiKeySecret = null;
  }

  internal void ClearApiKeyRotationSecrets()
  {
    ClearPendingApiKeyRotationSecret();
    ApiKeySecret = null;
  }

  internal void DisposeApiKeyRotationState()
  {
    Volatile.Write(ref settingsViewModelDisposed, 1);
    ClearApiKeyRotationSecrets();
  }

  private bool PublishOrHoldRotatedApiKeySecret(string ownerId, string rawKey, int ownerInvalidationGeneration)
  {
    if (Volatile.Read(ref settingsViewModelDisposed) != 0 ||
        ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration)) return false;
    var currentGeneration = Volatile.Read(ref settingsLoadGeneration);
    if (Volatile.Read(ref settingsIdentityLoadGeneration) == currentGeneration)
    {
      if (ownerId != currentUserIdOrSlug) return false;
      ApiKeySecret = rawKey;
      return true;
    }
    pendingRotatedApiKeyOwnerId = ownerId;
    pendingRotatedApiKeySecret = rawKey;
    return false;
  }
}
