using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed record ModerationRouteContext(
    string Path,
    UiMessageKey TitleKey,
    ModerationRouteKind RouteKind,
    bool Mine = false,
    string? ApiPath = null,
    string TransparencyRange = ModerationTransparencyRange.Default)
{
  public string Title => UiLocalization.English.Localize(TitleKey);
}
