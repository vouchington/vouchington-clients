using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed record CommunitySummaryRow(
    string Id,
    UiText TitleText,
    UiText SubtitleText,
    UiText? DetailText,
    IUiLocalization Localization)
{
  public CommunitySummaryRow(string id, string title, string subtitle, string? detail = null)
      : this(
          id,
          UiText.Verbatim(title),
          UiText.Verbatim(subtitle),
          detail is null ? null : UiText.Verbatim(detail),
          UiLocalization.English)
  {
  }

  public string Title => Localization.Resolve(TitleText);

  public string Subtitle => Localization.Resolve(SubtitleText);

  public string? Detail => DetailText is UiText detail ? Localization.Resolve(detail) : null;
}
