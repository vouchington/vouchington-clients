using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static partial class ModerationAppealCardPresentation
{
  public static string? RemovalTitle(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return appeal.TargetContext is ModerationAppealPostRemovalContext removal &&
        !string.IsNullOrWhiteSpace(removal.Title)
        ? UiCopy.Format(
            UiMessageKey.NativeSwiftModerationAppealsPostContext,
            ("value", UiText.UserContent(removal.Title)))
        : null;
  }

  private static void AddTargetContext(
      List<string> lines,
      ModerationAppealTargetContext? context)
  {
    switch (context)
    {
      case ModerationAppealWarningContext warning:
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsDecisionContext,
            "value", warning.PublicMessage);
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsCommunityContext,
            "value", warning.Community?.Name);
        break;
      case ModerationAppealCommunityBanContext ban:
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsDecisionContext,
            "value", ban.Reason);
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsCommunityContext,
            "value", ban.Community.Name);
        break;
      case ModerationAppealPostRemovalContext removal:
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsDecisionContext,
            "value", removal.PublicReason);
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsCommunityContext,
            "value", removal.Community?.Name);
        break;
      case ModerationAppealSuspensionContext suspension:
        AddValue(lines, UiMessageKey.NativeSwiftModerationAppealsDecisionContext,
            "value", suspension.Reason);
        break;
    }
  }

  private static void AddValue(
      List<string> lines,
      UiMessageKey key,
      string parameter,
      string? value)
  {
    if (string.IsNullOrWhiteSpace(value)) return;
    lines.Add(UiCopy.Format(key, (parameter, UiText.UserContent(value))));
  }

  private static string ActorLabel(ModerationActorSummary actor) =>
      FirstNonBlank(actor.VerifiedDisplayName, actor.Username) ?? actor.Id;

  private static string? FirstNonBlank(params string?[] values) =>
      values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
