using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed record ModerationRow(
    string Id,
    UiText TitleText,
    UiText DetailText,
    IUiLocalization Localization,
    string Icon = "shield-alert")
{
  public string Title => Localization.Resolve(TitleText);

  public string Detail => Localization.Resolve(DetailText);
}
