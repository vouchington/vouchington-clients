using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private sealed record UndismissedApiKeyDisclosure(
      string OwnerId,
      int OwnerInvalidationGeneration,
      string RawKey,
      UiMessageKey? NoticeKey);

  private UndismissedApiKeyDisclosure? retainedApiKeyDisclosure;

  private void RetainVisibleApiKeyDisclosureForRefresh()
  {
    if (retainedApiKeyDisclosure is not null || ApiKeySecret is not { Length: > 0 } rawKey ||
        currentUserIdOrSlug is not { } ownerId) return;
    retainedApiKeyDisclosure = new UndismissedApiKeyDisclosure(
        ownerId, Volatile.Read(ref apiKeyOwnerInvalidationGeneration), rawKey, apiKeyRotationNoticeKey);
  }

  private void RestoreVisibleApiKeyDisclosureForOwner(string ownerId)
  {
    if (retainedApiKeyDisclosure is not { } disclosure ||
        disclosure.OwnerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration) ||
        !string.Equals(disclosure.OwnerId, ownerId, StringComparison.Ordinal)) return;
    retainedApiKeyDisclosure = null;
    ApiKeySecret = disclosure.RawKey;
    apiKeyRotationNoticeKey = disclosure.NoticeKey;
    OnPropertyChanged(nameof(ApiKeyRotationNotice));
  }
}
