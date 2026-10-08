using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationReportsPage
{
  private static VerticalStackLayout CardStack(params UiText?[] lines)
  {
    var stack = new VerticalStackLayout { Spacing = 5 };
    foreach (var line in lines)
      if (line is { } value) AddLine(stack, value);
    if (stack.Children.FirstOrDefault() is Label title) title.FontAttributes = FontAttributes.Bold;
    return stack;
  }

  private static Border CardBorder(View content) => new()
  {
    Content = content,
    Padding = 12,
    Stroke = Colors.LightGray,
  };

  private static UiText Reasons(IReadOnlyList<ModerationReportReasonBreakdown> reasons) =>
      UiText.Verbatim(string.Join(" · ", reasons.Select(reason => UiCopy.Format(
          UiMessageKey.NativeSwiftModerationReportsReasonCount,
          ("reason", reason.Reason),
          ("count", reason.Count)))));

  private static UiText Indicators(ModerationReportIndicators indicators) =>
      UiText.Localized(
          UiMessageKey.NativeDotnetModerationRebasedSignals,
          ("signals", string.Join(", ", new[]
          {
            indicators.ContentHashDuplicate
                ? UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedDuplicateContentSignal)
                : null,
            indicators.EmbeddingsSimilarity
                ? UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedSimilarContentSignal)
                : null,
            indicators.VelocitySpike
                ? UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedReportSpikeSignal)
                : null,
          }.Where(value => value is not null))));

  private static void AddJudgement(VerticalStackLayout content, ModerationReportJudgement? judgement)
  {
    if (judgement is null) return;
    AddLine(content, UiText.Localized(
        UiMessageKey.NativeSwiftModerationReportsAiJudgement,
        ("action", judgement.RecommendedAction)));
    AddLine(content, UiText.Localized(
        UiMessageKey.NativeDotnetModerationRebasedJudgementPublicResponse,
        ("response", judgement.PublicResponse)));
    AddLine(content, UiText.Localized(
        UiMessageKey.NativeDotnetModerationRebasedJudgementStaffNote,
        ("note", judgement.InternalResponse)));
    if (judgement.IsStale)
      AddLine(content, UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedJudgementStale));
  }

  private static void AddBanEvasion(VerticalStackLayout content, CommunityBanEvasionContext? context)
  {
    if (context is null) return;
    AddLine(content, UiText.Localized(
        UiMessageKey.NativeDotnetModerationRebasedBanEvasionDetail,
        ("community", context.CommunitySlug),
        ("score", UiCopy.CurrentLocalization.FormatPercent((decimal)context.Score)),
        ("source", context.SourceUsername ?? context.SourceUserId)));
  }

  private static UiText Target(string? label, string entityType, string entityId) =>
      string.IsNullOrWhiteSpace(label)
          ? UiText.Localized(
              UiMessageKey.NativeDotnetModerationRebasedTargetLabel,
              ("type", entityType),
              ("id", entityId))
          : UiText.UserContent(label);

  private static UiText ReportSummary(
      ModerationReportStatus status,
      int reportCount,
      DateTimeOffset createdAt) =>
      UiText.Localized(
          UiMessageKey.NativeDotnetModerationRebasedReportCardSummary,
          ("status", StatusLabel(status)),
          ("reports", UiCopy.Format(
              UiMessageKey.NativeSwiftModerationReportsReportCount,
              ("count", reportCount))),
          ("date", UiCopy.FormatDateTime(createdAt)));

  private static UiText FirstLatest(DateTimeOffset first, DateTimeOffset latest) =>
      UiText.Localized(
          UiMessageKey.NativeDotnetModerationRebasedFirstLatest,
          ("first", UiCopy.FormatDateTime(first)),
          ("latest", UiCopy.FormatDateTime(latest)));

  private static void AddLine(VerticalStackLayout content, UiText text) =>
      content.Children.Add(new Label { Text = UiCopy.Resolve(text) });

  private static void AddAuthoredContent(VerticalStackLayout content, AuthoredContentText? source)
  {
    if (string.IsNullOrWhiteSpace(source?.Text)) return;
    var language = AuthoredContentLanguage.Resolve(source.DeclaredLanguage, source.LinguaRsDetectedLanguage);
    var label = new Label { Text = source.Text };
    if (language.Direction is { } direction)
      label.FlowDirection = direction == AuthoredTextDirection.RightToLeft
          ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    content.Children.Add(label);
  }
}
