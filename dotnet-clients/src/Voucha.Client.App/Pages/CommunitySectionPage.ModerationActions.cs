using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildModerationActions()
  {
    var selection = new Picker
    {
      AutomationId = "community-moderation-target",
      ItemDisplayBinding = new Binding(nameof(CommunityModerationRow.Title)),
    };
    selection.SetBinding(Picker.ItemsSourceProperty, nameof(CommunityDetailViewModel.ModerationActionRows));
    selection.SetDynamicResource(Picker.TitleProperty, UiMessageKey.NativeDotnetCsharpCommunitiesModerationResults.Value);
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    var reportStatus = LocalizedPicker(UiMessageKey.NativeDotnetModerationRebasedReviewed,
        UiMessageKey.NativeSwiftModerationReportsDismissed);
    reportStatus.AutomationId = "community-report-resolution-status";
    reportStatus.SelectedIndex = 0;
    var postStatus = LocalizedPicker(UiMessageKey.NativeDotnetModerationApproved,
        UiMessageKey.NativeDotnetModerationRejected);
    postStatus.AutomationId = "community-post-review-status";
    postStatus.SelectedIndex = 0;
    var actions = new VerticalStackLayout { Spacing = 8, Children = { selection, reason, reportStatus, postStatus } };
    void Add(UiMessageKey key, Func<CommunityModerationRow, bool> enabled,
        Func<CommunityModerationRow, CancellationToken, Task> action)
    {
      var button = ActionButton(key);
      button.IsEnabled = false;
      selection.SelectedIndexChanged += (_, _) =>
          button.IsEnabled = selection.SelectedItem is CommunityModerationRow row && enabled(row);
      button.Clicked += async (_, _) =>
      {
        if (selection.SelectedItem is CommunityModerationRow row && enabled(row))
          await InvokeAndRenderAsync(ct => action(row, ct)).ConfigureAwait(true);
      };
      actions.Children.Add(button);
    }
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesModerationResults, row => row.PostId is not null,
        async (row, ct) => { await viewModel.LoadModerationResultsAsync(row.PostId!, ct).ConfigureAwait(true); });
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesWarn, row => row.UserId is not null,
        async (row, ct) => { await viewModel.IssueWarningAsync(row.UserId!, reason.Text ?? string.Empty,
            reportId: row.ReportId, cancellationToken: ct).ConfigureAwait(true); });
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesReviewPost, row => row.PostId is not null,
        async (row, ct) => { await viewModel.ReviewPostAsync(row.PostId!, postStatus.SelectedIndex == 1 ? "rejected" : "approved", OptionalText(reason), ct).ConfigureAwait(true); });
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesResolveReport, row => row.ReportId is not null,
        async (row, ct) => { await viewModel.ResolveModerationReportAsync(row.ReportId!, reportStatus.SelectedIndex == 1 ? "dismissed" : "reviewed", ct).ConfigureAwait(true); });
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesConfirmEvasion, row => row.UserId is not null && row.HasBanEvasion,
        async (row, ct) => { await viewModel.ConfirmBanEvasionAsync(row.UserId!, ct).ConfigureAwait(true); });
    Add(UiMessageKey.NativeDotnetCsharpCommunitiesDismissEvasion, row => row.UserId is not null && row.HasBanEvasion,
        async (row, ct) => { await viewModel.DismissBanEvasionAsync(row.UserId!, ct).ConfigureAwait(true); });
    var recent = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRecentActions);
    recent.Clicked += async (_, _) => await InvokeAndRenderAsync(ct =>
        viewModel.LoadAutomodRecentActionsAsync(cancellationToken: ct)).ConfigureAwait(true);
    actions.Children.Add(recent);
    return actions;
  }
}
