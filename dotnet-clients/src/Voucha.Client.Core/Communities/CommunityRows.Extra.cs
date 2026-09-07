using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed record CommunitySummaryRow(
    string Id,
    UiText TitleText,
    UiText SubtitleText,
    UiText? DetailText,
    IUiLocalization Localization,
    UrlEmbedPreview? EmbedPreview = null,
    string? TitleDeclaredLanguage = null,
    string? TitleDetectedLanguage = null,
    AuthoredContentText? Content = null)
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

  public string? TitleFlowDirection =>
      AuthoredContentLanguage.Resolve(TitleDeclaredLanguage, TitleDetectedLanguage).Direction?.ToString();

  public string? AuthoredContent => Content?.Text;

  public string? AuthoredContentFlowDirection => Content is null
      ? null
      : AuthoredContentLanguage.Resolve(
          Content.DeclaredLanguage,
          Content.LinguaRsDetectedLanguage).Direction?.ToString();

  public bool HasAuthoredContent => !string.IsNullOrWhiteSpace(AuthoredContent);
}
