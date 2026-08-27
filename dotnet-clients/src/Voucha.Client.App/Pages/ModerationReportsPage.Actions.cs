using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationReportsPage
{
  private View BulkActions()
  {
    if (!viewModel.IsStaff) return new BoxView { IsVisible = false };
    var row = new HorizontalStackLayout { Spacing = 8 };
    row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsDismissSelected, () => ConfirmBulkActionAsync(
        UiMessageKey.NativeDotnetModerationRebasedDismissSelectedTitle,
        UiMessageKey.NativeDotnetModerationRebasedDismissSelectedMessage,
        UiMessageKey.NativeSwiftModerationReportsDismiss,
        () => viewModel.DismissSelectedAsync())));
    if (viewModel.IsAdministrator)
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsRemoveSelected, () => ConfirmBulkActionAsync(
          UiMessageKey.NativeDotnetModerationRebasedRemoveSelectedTitle,
          UiMessageKey.NativeDotnetModerationRebasedRemoveSelectedMessage,
          UiMessageKey.NativeSwiftModerationReportsRemoveContent,
          () => viewModel.RemoveSelectedTargetsAsync())));
    return row;
  }

  private View ClusterActions(string clusterId)
  {
    var row = new HorizontalStackLayout { Spacing = 8 };
    if (viewModel.CanDismissCluster(clusterId))
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsDismissLoadedReports, () => ConfirmBulkActionAsync(
          UiMessageKey.NativeDotnetModerationRebasedDismissLoadedTitle,
          UiMessageKey.NativeDotnetModerationRebasedDismissLoadedMessage,
          UiMessageKey.NativeSwiftModerationReportsDismiss,
          () => viewModel.DismissClusterAsync(clusterId))));
    if (viewModel.CanRemoveClusterTargets(clusterId))
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsRemoveLoadedPosts, () => ConfirmBulkActionAsync(
          UiMessageKey.NativeDotnetModerationRebasedRemoveLoadedTitle,
          UiMessageKey.NativeDotnetModerationRebasedRemoveLoadedMessage,
          UiMessageKey.NativeSwiftModerationReportsRemoveContent,
          () => viewModel.RemoveClusterTargetsAsync(clusterId))));
    return row.Children.Count == 0 ? new BoxView { IsVisible = false } : row;
  }

  private async Task ConfirmBulkActionAsync(
      UiMessageKey title,
      UiMessageKey message,
      UiMessageKey accept,
      Func<Task<ModerationBulkResult>> action)
  {
    if (!await DisplayAlertAsync(
        UiCopy.Localize(title),
        UiCopy.Localize(message),
        UiCopy.Localize(accept),
        UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true)) return;
    await action().ConfigureAwait(true);
  }

  private View ReportActions(StaffModerationReport report)
  {
    var content = new VerticalStackLayout { Spacing = 6 };
    var select = new CheckBox
    {
      IsChecked = viewModel.IsSelected(report.Id),
      IsEnabled = !viewModel.IsActionInFlight(report.Id),
    };
    select.CheckedChanged += (_, _) => viewModel.ToggleSelection(report.Id);
    content.Children.Add(new HorizontalStackLayout
    {
      Spacing = 4,
      Children =
      {
        select,
        UiCopy.Bind(
            new Label { VerticalTextAlignment = TextAlignment.Center },
            Label.TextProperty,
            UiMessageKey.NativeDotnetModerationRebasedSelect),
      },
    });
    var row = new HorizontalStackLayout { Spacing = 8 };
    if (viewModel.CanResolve(report))
    {
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsReview, () => viewModel.ReviewAsync(report.Id)));
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsDismiss, () => viewModel.DismissAsync(report.Id)));
    }
    row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsRerunJudgement, () => viewModel.RerunJudgementAsync(report.Id)));
    if (viewModel.CanHandleBanEvasion(report))
    {
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsConfirmBanEvasion, () => viewModel.ConfirmBanEvasionAsync(report.Id)));
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsDismissBanEvasion, () => viewModel.DismissBanEvasionAsync(report.Id)));
    }
    else if (viewModel.CanIssueWarning(report))
    {
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsIssueWarning, () => IssueWarningAsync(report.Id)));
    }
    if (viewModel.CanRemove(report))
      row.Children.Add(ActionButton(UiMessageKey.NativeSwiftModerationReportsRemoveContent, () => ConfirmRemoveAsync(report.Id)));
    content.Children.Add(row);
    return content;
  }

  private async Task IssueWarningAsync(string reportId)
  {
    var reason = await DisplayPromptAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsIssueWarning),
        UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedWarningReasonPrompt),
        maxLength: 1000).ConfigureAwait(true);
    if (string.IsNullOrWhiteSpace(reason)) return;
    var publicMessage = await DisplayPromptAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsPublicMessageOptional),
        UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedOptionalPublicMessagePrompt),
        maxLength: 2000).ConfigureAwait(true);
    await viewModel.IssueWarningAsync(reportId, reason, publicMessage).ConfigureAwait(true);
  }

  private async Task ConfirmRemoveAsync(string reportId)
  {
    if (!await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedRemoveTargetTitle),
        UiCopy.Localize(UiMessageKey.NativeDotnetModerationRebasedRemoveTargetMessage),
        UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsRemoveContent),
        UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true)) return;
    await viewModel.RemoveTargetAsync(reportId).ConfigureAwait(true);
  }

  private Button ActionButton(UiMessageKey text, Func<Task> action)
  {
    var button = UiCopy.Bind(
        new Button { IsEnabled = !viewModel.IsQueueInteractionBlocked },
        Button.TextProperty,
        text);
    button.Clicked += async (_, _) =>
    {
      button.IsEnabled = false;
      try { await action().ConfigureAwait(true); }
      finally
      {
        RenderQueue();
        button.IsEnabled = true;
      }
    };
    return button;
  }

  private static void AddTargetLink(VerticalStackLayout content, string? targetPath)
  {
    if (string.IsNullOrWhiteSpace(targetPath)) return;
    var button = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        UiMessageKey.NativeDotnetModerationRebasedOpenTarget);
    button.Clicked += async (_, _) =>
    {
      if (Shell.Current is AppShell appShell)
        await appShell.OpenNativePathAsync(targetPath).ConfigureAwait(true);
    };
    content.Children.Add(button);
  }
}
