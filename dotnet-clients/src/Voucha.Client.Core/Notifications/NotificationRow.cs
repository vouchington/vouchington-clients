using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

public sealed record NotificationRow(
    string Id,
    NotificationText Text,
    DateTimeOffset? CreatedAt,
    string? TargetPath,
    string? EntityType,
    bool IsRead,
    IUiLocalization? Localization = null,
    string? Kind = null)
{
  public bool IsUnread => !IsRead;

  private IUiLocalization L => Localization ?? UiLocalization.English;

  public string LocalizedTitle => L.Resolve(Text.Title);

  /// <summary>The body paragraphs joined by a blank line; empty for a title-only row.</summary>
  public string LocalizedBody => string.Join(
      "\n\n",
      Text.Body.Select(L.Resolve).Where(paragraph => paragraph.Length > 0));

  public bool HasBody => LocalizedBody.Length > 0;

  public string ReadStateLabel => L.Localize(IsRead ? UiMessageKey.NativeDotnetDynamicRead : UiMessageKey.NativeDotnetDynamicUnread);

  public string MarkReadActionLabel => L.Localize(IsRead ? UiMessageKey.NativeDotnetDynamicRead : UiMessageKey.NativeDotnetDynamicMarkRead);

  public NotificationRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}
