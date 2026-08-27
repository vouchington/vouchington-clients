using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed record ReviewQueueMediaRow(
    Uri Source,
    UiText CaptionText,
    int OrderIndex,
    IUiLocalization Localization)
{
  public string CaptionPresentation => Localization.Resolve(CaptionText);
  public bool HasCaption => !string.IsNullOrWhiteSpace(CaptionPresentation);
}
