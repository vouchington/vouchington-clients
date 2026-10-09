namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private int settingsIdentityLoadGeneration;
  private int apiKeyOwnerInvalidationGeneration;
  private int settingsViewModelDisposed;
  private long apiKeySecretOperationSequence;
  private long activeApiKeySecretOperation;
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
    NotifyApiKeySecretAdmission();
    RestoreVisibleApiKeyDisclosureForOwner(ownerId);
    if (pendingCreatedApiKeySecret is { } createdSecret)
    {
      var createdOwnerId = pendingCreatedApiKeyOwnerId;
      var pendingCreatedOperation = pendingCreatedApiKeyOperation;
      ClearPendingCreatedApiKeySecret();
      if (createdOwnerId == ownerId && IsCurrentApiKeySecretOperation(pendingCreatedOperation))
      {
        ApiKeySecret = createdSecret;
        CompleteApiKeySecretOperation(pendingCreatedOperation);
      }
    }
    if (pendingRotatedApiKeySecret is not { } secret) return;
    var pendingOwnerId = pendingRotatedApiKeyOwnerId;
    var pendingOperation = pendingRotatedApiKeyOperation;
    ClearPendingApiKeyRotationSecret();
    if (pendingOwnerId != ownerId || !IsCurrentApiKeySecretOperation(pendingOperation)) return;
    ApiKeySecret = secret;
    apiKeyRotationNoticeKey = Localization.UiMessageKey.NativeApiKeysRotated;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
    CompleteApiKeySecretOperation(pendingOperation);
  }

  private void BeginApiKeyOwnerIdentityLoad()
  {
    Volatile.Write(ref settingsIdentityLoadGeneration, 0);
    NotifyApiKeySecretAdmission();
  }

  private bool IsCurrentApiKeyOwner(string ownerId, int expectedGeneration) =>
      Volatile.Read(ref settingsViewModelDisposed) == 0 &&
      expectedGeneration == Volatile.Read(ref apiKeyOwnerInvalidationGeneration) &&
      string.Equals(ownerId, currentUserIdOrSlug, StringComparison.Ordinal);

  private bool IsApiKeySecretOperationBusy => Volatile.Read(ref activeApiKeySecretOperation) != 0 ||
      ApiKeySecret is { Length: > 0 } || retainedApiKeyDisclosure is not null;

  private bool CanStartApiKeySecretOperation => Volatile.Read(ref settingsViewModelDisposed) == 0 &&
      currentUserIdOrSlug is not null &&
      Volatile.Read(ref settingsIdentityLoadGeneration) == Volatile.Read(ref settingsLoadGeneration) &&
      !IsApiKeySecretOperationBusy;

  private bool TryBeginApiKeySecretOperation(out long operation)
  {
    operation = 0;
    if (IsApiKeySecretOperationBusy) return false;
    var ticket = Interlocked.Increment(ref apiKeySecretOperationSequence);
    if (Interlocked.CompareExchange(ref activeApiKeySecretOperation, ticket, 0) != 0) return false;
    if (ApiKeySecret is { Length: > 0 } || retainedApiKeyDisclosure is not null)
    {
      Interlocked.CompareExchange(ref activeApiKeySecretOperation, 0, ticket);
      return false;
    }
    operation = ticket;
    ClearPendingApiKeyRotationSecret();
    ClearPendingCreatedApiKeySecret();
    ApiKeySecret = null;
    NotifyApiKeySecretAdmission();
    return true;
  }

  private void CompleteApiKeySecretOperation(long operation)
  {
    if (Volatile.Read(ref activeApiKeySecretOperation) != operation ||
        pendingCreatedApiKeyOperation == operation || pendingRotatedApiKeyOperation == operation) return;
    Interlocked.CompareExchange(ref activeApiKeySecretOperation, 0, operation);
    NotifyApiKeySecretAdmission();
  }

  private void NotifyApiKeySecretAdmission()
  {
    OnPropertyChanged(nameof(CanCreateApiKey));
    OnPropertyChanged(nameof(LocalizedApiKeys));
  }

  private bool IsCurrentApiKeySecretOperation(long operation) =>
      operation != 0 && operation == Volatile.Read(ref activeApiKeySecretOperation);

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
    Volatile.Write(ref activeApiKeySecretOperation, 0);
    ClearPendingApiKeyRotationSecret();
    ClearPendingCreatedApiKeySecret();
    retainedApiKeyDisclosure = null;
    ApiKeySecret = null;
    NotifyApiKeySecretAdmission();
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
