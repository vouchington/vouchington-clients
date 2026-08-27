using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

public sealed record NotificationRow(
    string Id,
    UiText TitleText,
    string UserContentBody,
    DateTimeOffset? CreatedAt,
    string? TargetPath,
    string? EntityType,
    bool IsRead,
    IUiLocalization? Localization = null)
{
  public bool IsUnread => !IsRead;

  private IUiLocalization L => Localization ?? UiLocalization.English;

  public string LocalizedTitle => L.Resolve(TitleText);

  public string ReadStateLabel => L.Localize(IsRead ? UiMessageKey.NativeDotnetDynamicRead : UiMessageKey.NativeDotnetDynamicUnread);

  public string MarkReadActionLabel => L.Localize(IsRead ? UiMessageKey.NativeDotnetDynamicRead : UiMessageKey.NativeDotnetDynamicMarkRead);

  public NotificationRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}
