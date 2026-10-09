namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private int settingsIdentityLoadGeneration;
  private int apiKeyOwnerInvalidationGeneration;
  private int settingsViewModelDisposed;
  private long apiKeySecretOperationSequence;
  private string? pendingRotatedApiKeyOwnerId;
  private string? pendingRotatedApiKeySecret;
  private long pendingRotatedApiKeyOperation;
  private string? pendingCreatedApiKeyOwnerId;
  private string? pendingCreatedApiKeySecret;
  private long pendingCreatedApiKeyOperation;

  private void ConfirmApiKeyOwnerIdentity(int generation, string ownerId)
  {
    if (currentUserIdOrSlug is { } previousOwnerId && previousOwnerId != ownerId)
      InvalidateApiKeyOwnerIdentity();
    currentUserIdOrSlug = ownerId;
    Volatile.Write(ref settingsIdentityLoadGeneration, generation);
    OnPropertyChanged(nameof(CanCreateApiKey));
    if (pendingCreatedApiKeySecret is { } createdSecret)
    {
      var createdOwnerId = pendingCreatedApiKeyOwnerId;
      var pendingCreatedOperation = pendingCreatedApiKeyOperation;
      ClearPendingCreatedApiKeySecret();
      if (createdOwnerId == ownerId && pendingCreatedOperation == Volatile.Read(ref apiKeySecretOperationSequence))
        ApiKeySecret = createdSecret;
    }
    if (pendingRotatedApiKeySecret is not { } secret) return;
    var pendingOwnerId = pendingRotatedApiKeyOwnerId;
    var pendingOperation = pendingRotatedApiKeyOperation;
    ClearPendingApiKeyRotationSecret();
    if (pendingOwnerId != ownerId || pendingOperation != Volatile.Read(ref apiKeySecretOperationSequence)) return;
    ApiKeySecret = secret;
    apiKeyRotationNoticeKey = Localization.UiMessageKey.NativeApiKeysRotated;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
  }

  private void BeginApiKeyOwnerIdentityLoad()
  {
    Volatile.Write(ref settingsIdentityLoadGeneration, 0);
    OnPropertyChanged(nameof(CanCreateApiKey));
  }

  private bool IsCurrentApiKeyOwner(string ownerId, int expectedGeneration) =>
      Volatile.Read(ref settingsViewModelDisposed) == 0 &&
      expectedGeneration == Volatile.Read(ref apiKeyOwnerInvalidationGeneration) &&
      string.Equals(ownerId, currentUserIdOrSlug, StringComparison.Ordinal);

  private long BeginApiKeySecretOperation()
  {
    var operation = Interlocked.Increment(ref apiKeySecretOperationSequence);
    ClearPendingApiKeyRotationSecret();
    ClearPendingCreatedApiKeySecret();
    ApiKeySecret = null;
    return operation;
  }

  private bool IsCurrentApiKeySecretOperation(long operation) =>
      operation == Volatile.Read(ref apiKeySecretOperationSequence);

  private void InvalidateApiKeyOwnerIdentity()
  {
    Interlocked.Increment(ref apiKeyOwnerInvalidationGeneration);
    rotatingApiKeyIds.Clear();
    currentUserIdOrSlug = null;
    Volatile.Write(ref settingsIdentityLoadGeneration, 0);
    OnPropertyChanged(nameof(CanCreateApiKey));
    ClearApiKeyRotationSecrets();
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
    pendingRotatedApiKeyOperation = 0;
  }

  private void ClearPendingCreatedApiKeySecret()
  {
    pendingCreatedApiKeyOwnerId = null;
    pendingCreatedApiKeySecret = null;
    pendingCreatedApiKeyOperation = 0;
  }

  private void PublishOrHoldCreatedApiKeySecret(string ownerId, string rawKey, int expectedGeneration, long operation)
  {
    if (!IsCurrentApiKeyOwner(ownerId, expectedGeneration) || !IsCurrentApiKeySecretOperation(operation)) return;
    if (Volatile.Read(ref settingsIdentityLoadGeneration) == Volatile.Read(ref settingsLoadGeneration))
    {
      ApiKeySecret = rawKey;
      return;
    }
    pendingCreatedApiKeyOwnerId = ownerId;
    pendingCreatedApiKeySecret = rawKey;
    pendingCreatedApiKeyOperation = operation;
  }

  internal void ClearApiKeyRotationSecrets()
  {
    Interlocked.Increment(ref apiKeySecretOperationSequence);
    ClearPendingApiKeyRotationSecret();
    ClearPendingCreatedApiKeySecret();
    ApiKeySecret = null;
  }

  internal void DisposeApiKeyRotationState()
  {
    Volatile.Write(ref settingsViewModelDisposed, 1);
    OnPropertyChanged(nameof(CanCreateApiKey));
    rotatingApiKeyIds.Clear();
    ClearApiKeyRotationSecrets();
  }

  private bool PublishOrHoldRotatedApiKeySecret(
      string ownerId, string rawKey, int ownerInvalidationGeneration, long operation)
  {
    if (Volatile.Read(ref settingsViewModelDisposed) != 0 ||
        ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration) ||
        !IsCurrentApiKeySecretOperation(operation)) return false;
    var currentGeneration = Volatile.Read(ref settingsLoadGeneration);
    if (Volatile.Read(ref settingsIdentityLoadGeneration) == currentGeneration)
    {
      if (ownerId != currentUserIdOrSlug) return false;
      ApiKeySecret = rawKey;
      return true;
    }
    pendingRotatedApiKeyOwnerId = ownerId;
    pendingRotatedApiKeySecret = rawKey;
    pendingRotatedApiKeyOperation = operation;
    return false;
  }
}
