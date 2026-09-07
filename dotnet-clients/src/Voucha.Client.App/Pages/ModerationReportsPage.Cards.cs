using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationReportsPage
{
  private void RenderQueue()
  {
    noticeLabel.Text = viewModel.Notice;
    errorLabel.Text = viewModel.ErrorMessage;
    loadMoreButton.IsVisible = viewModel.HasNextPage;
    loadMoreButton.IsEnabled = !viewModel.IsQueueInteractionBlocked;
    statusPicker.IsEnabled = !viewModel.IsQueueInteractionBlocked;
    modePicker.IsEnabled = !viewModel.IsQueueInteractionBlocked;
    sortPicker.IsEnabled = !viewModel.IsQueueInteractionBlocked;
    applyingPickerState = true;
    modePicker.SelectedIndex = ModeIndex(viewModel.Mode);
    applyingPickerState = false;
    sortPicker.IsVisible = viewModel.IsStaff && viewModel.Mode == ModerationReportsMode.Flat;
    queue.Children.Clear();
    if (viewModel.IsLoading && viewModel.StaffReports.Count == 0 &&
        viewModel.MemberReports.Count == 0 && viewModel.Clusters.Count == 0)
    {
      queue.Children.Add(new ActivityIndicator { IsRunning = true });
      return;
    }
    if (viewModel.Mode == ModerationReportsMode.Grouped && viewModel.IsStaff)
    {
      foreach (var duplicate in viewModel.DuplicateClusters.Where(viewModel.HasActiveReports))
        queue.Children.Add(DuplicateCard(duplicate));
      foreach (var cluster in viewModel.Clusters.Where(item => viewModel.ReportsForCluster(item).Count > 0))
        queue.Children.Add(ClusterCard(cluster));
    }
    else if (viewModel.IsStaff)
    {
      foreach (var report in viewModel.StaffReports) queue.Children.Add(StaffReportCard(report));
    }
    else
    {
      foreach (var report in viewModel.MemberReports) queue.Children.Add(MemberReportCard(report));
    }
    if (queue.Children.Count == 0)
      queue.Children.Add(UiCopy.Bind(
          new Label(),
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationReportsNoReportsMessage));
  }

  private View DuplicateCard(StaffModerationReportDuplicateCluster duplicate)
  {
    var counts = viewModel.PresentationCounts(duplicate);
    var content = CardStack(
        UiText.ProtocolValue(duplicate.Signal.Replace('_', ' ')),
        UiText.Localized(
            UiMessageKey.NativeSwiftModerationReportsPostReportCounts,
            ("posts", UiCopy.Format(UiMessageKey.NativeSwiftModerationReportsPostCount, ("count", counts.Posts))),
            ("reports", UiCopy.Format(UiMessageKey.NativeSwiftModerationReportsReportCount, ("count", counts.Reports)))),
        Reasons(viewModel.PresentationReasons(duplicate)),
        FirstLatest(duplicate.FirstReportedAt, duplicate.LastReportedAt));
    content.Children.Add(ClusterActions(duplicate.Id));
    foreach (var cluster in duplicate.Clusters.Where(item => viewModel.ReportsForCluster(item).Count > 0))
      content.Children.Add(ClusterCard(cluster));
    return CardBorder(content);
  }

  private View ClusterCard(StaffModerationReportEntityCluster cluster)
  {
    var reports = viewModel.ReportsForCluster(cluster);
    var content = CardStack(
        Target(cluster.TargetLabel, cluster.EntityType, cluster.EntityId),
        UiText.Localized(
            UiMessageKey.NativeSwiftModerationReportsClusterSummary,
            ("reports", UiCopy.Format(
                UiMessageKey.NativeSwiftModerationReportsReportCount,
                ("count", viewModel.PresentationReportCount(cluster)))),
            ("reporters", UiCopy.Format(
                UiMessageKey.NativeSwiftModerationReportsReporterCount,
                ("count", cluster.ReporterCount)))),
        Reasons(viewModel.PresentationReasons(cluster)),
        Indicators(cluster.Indicators),
        FirstLatest(cluster.FirstReportedAt, cluster.LastReportedAt));
    AddAuthoredContent(content, cluster.TargetContent);
    AddTargetLink(content, cluster.TargetPath ?? cluster.AdminActionPath);
    content.Children.Add(ClusterActions(cluster.Id));
    foreach (var report in reports) content.Children.Add(StaffReportCard(report));
    return CardBorder(content);
  }

  private View StaffReportCard(StaffModerationReport report)
  {
    var content = CardStack(
        Target(report.TargetLabel, report.EntityType, report.EntityId),
        ReportSummary(report.Status, report.ReportCount, report.CreatedAt),
        UiText.Localized(UiMessageKey.NativeSwiftModerationReportsReason, ("reason", report.Reason)),
        report.TargetAvailable == false
            ? UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable)
            : null,
        report.TargetIsRestricted
            ? UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedRestrictedTarget)
            : null,
        UiText.Localized(
            UiMessageKey.NativeSwiftModerationReportsReporter,
            ("reporter", report.ReporterUsername ?? report.ReporterUserId)),
        string.IsNullOrWhiteSpace(report.Note)
            ? null
            : UiText.Localized(UiMessageKey.NativeSwiftModerationReportsNote, ("note", report.Note)),
        report.ResolvedById is null
            ? null
            : UiText.Localized(
                UiMessageKey.NativeDotnetModerationRebasedResolvedBy,
                ("actor", report.ResolvedById)));
    AddTargetLink(content, report.TargetPath ?? report.AdminActionPath);
    AddAuthoredContent(content, report.TargetContent);
    AddJudgement(content, report.Judgement);
    AddBanEvasion(content, report.CommunityBanEvasion);
    if (report.PostModerationContext is { } context)
      AddLine(content, UiText.Localized(
          UiMessageKey.NativeSwiftModerationReportsModerationContext,
          ("context", context.GetRawText())));
    content.Children.Add(ReportActions(report));
    if (viewModel.RowErrors.TryGetValue(report.Id, out var error))
      content.Children.Add(new Label { Text = error, TextColor = Colors.IndianRed });
    return CardBorder(content);
  }

  private static View MemberReportCard(MemberModerationReport report)
  {
    var content = CardStack(
        Target(report.TargetLabel, report.EntityType, report.EntityId),
        ReportSummary(report.Status, report.ReportCount, report.CreatedAt),
        UiText.Localized(UiMessageKey.NativeSwiftModerationReportsReason, ("reason", report.Reason)),
        report.TargetAvailable == false
            ? UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable)
            : null,
        report.ReviewedAt is null
            ? null
            : UiText.Localized(
                UiMessageKey.NativeDotnetModerationRebasedReviewed,
                ("date", UiCopy.FormatDateTime(report.ReviewedAt.Value))));
    AddTargetLink(content, report.TargetPath);
    AddAuthoredContent(content, report.TargetContent);
    if (report.PostModerationContext is { } context)
      AddLine(content, UiText.Localized(
          UiMessageKey.NativeDotnetModerationRebasedContext,
          ("context", context.GetRawText())));
    return CardBorder(content);
  }

}
