using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>
/// The sentence a notification row shows. Each <see cref="Body"/> entry is one paragraph; an empty
/// body means the row has only a title.
/// </summary>
public sealed record NotificationText(UiText Title, IReadOnlyList<UiText> Body)
{
  public static NotificationText Generic { get; } =
      TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindGenericTitle));

  public static NotificationText TitleOnly(UiText title) => new(title, []);

  public static NotificationText Of(UiText title, UiText body) => new(title, [body]);
}
