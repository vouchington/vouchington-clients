using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View ApplicationRowTemplate()
  {
    var applicant = new Label { FontAttributes = FontAttributes.Bold, AutomationId = "community-application-applicant" };
    applicant.SetBinding(Label.TextProperty, ApplicationRowValueBinding(nameof(CommunityApplicationRow.ApplicantText)));
    var status = new Label { AutomationId = "community-application-status" };
    status.SetBinding(Label.TextProperty, ApplicationRowValueBinding(nameof(CommunityApplicationRow.StatusText)));
    var created = new Label { AutomationId = "community-application-created" };
    created.SetBinding(Label.TextProperty, ApplicationRowValueBinding(nameof(CommunityApplicationRow.CreatedAt), "dateTime"));
    var count = new Label { AutomationId = "community-application-answer-count" };
    count.SetBinding(Label.TextProperty, ApplicationRowValueBinding(nameof(CommunityApplicationRow.AnswerCount), "number"));
    var preview = new Label();
    preview.SetBinding(Label.TextProperty, nameof(CommunityApplicationRow.Message));
    var open = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesReview);
    open.AutomationId = "community-application-detail";
    open.Clicked += async (_, _) =>
    {
      if (open.BindingContext is CommunityApplicationRow row)
        await Navigation.PushModalAsync(new CommunityApplicationDetailPage(viewModel.CreateApplicationDetail(row.Id))).ConfigureAwait(true);
    };
    var approve = ActionButton(UiMessageKey.NativeSwiftModerationReportsReviewQueueApprove);
    approve.AutomationId = "community-application-approve";
    approve.Clicked += async (_, _) =>
    {
      if (approve.BindingContext is CommunityApplicationRow row && CanReviewApplicationRow(row))
        await InvokeAndRenderAsync(ct => viewModel.ReviewApplicationAsync(row.Id, "approved", cancellationToken: ct)).ConfigureAwait(true);
    };
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    reason.AutomationId = "community-application-rejection-reason";
    var reject = ActionButton(UiMessageKey.NativeSwiftModerationReportsReviewQueueReject);
    reject.AutomationId = "community-application-reject";
    reject.Clicked += async (_, _) =>
    {
      if (reject.BindingContext is CommunityApplicationRow row && CanReviewApplicationRow(row))
        await InvokeAndRenderAsync(ct => viewModel.ReviewApplicationAsync(row.Id, "rejected", OptionalText(reason), ct)).ConfigureAwait(true);
    };
    var layout = new VerticalStackLayout
    {
      Spacing = 4,
      Children = { applicant, status, created, preview, reason, new HorizontalStackLayout { Spacing = 4, Children = { new Label { Text = "💬" }, count } }, new HorizontalStackLayout { Children = { open, approve, reject } } },
    };
    layout.BindingContextChanged += (_, _) =>
    {
      if (layout.BindingContext is not CommunityApplicationRow row) return;
      reason.Text = null;
      approve.IsVisible = reject.IsVisible = reason.IsVisible = row.Status == "pending" && viewModel.CanModerateCommunity;
      approve.IsEnabled = reject.IsEnabled = CanReviewApplicationRow(row);
    };
    return layout;
  }

  private bool CanReviewApplicationRow(CommunityApplicationRow row) =>
      viewModel.SelectedSection == CommunityDetailSurfaceSection.Applications &&
      viewModel.CanModerateCommunity && !viewModel.IsLoading &&
      viewModel.ApplicationRows.Any(current => current.Id == row.Id && current.Status == "pending");

}
