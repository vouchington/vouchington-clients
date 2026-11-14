using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public static class ModerationLabels
{
  public static string Person(string? displayName, string? username, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    return Available(displayName) ?? Available(username) ?? Unavailable(localization);
  }

  public static string Entity(string? label, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    return Available(label) ?? Unavailable(localization);
  }

  private static string? Available(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static string Unavailable(IUiLocalization localization) =>
      localization.Localize(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable);
}
