using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View ApplicationRowTemplate()
  {
    var applicant = new Label { FontAttributes = FontAttributes.Bold };
    applicant.SetBinding(Label.TextProperty, nameof(CommunityApplicationRow.Applicant));
    var status = new Label();
    status.SetBinding(Label.TextProperty, nameof(CommunityApplicationRow.StatusLabel));
    var created = new Label();
    var count = new Label();
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
      created.Text = UiCopy.FormatDateTime(row.CreatedAt);
      count.Text = UiCopy.FormatNumber(row.AnswerCount);
      reason.Text = null;
      approve.IsVisible = reject.IsVisible = reason.IsVisible = row.Status == "pending" && viewModel.CanModerateCommunity;
      approve.IsEnabled = reject.IsEnabled = CanReviewApplicationRow(row);
    };
    return layout;
  }

  private bool CanReviewApplicationRow(CommunityApplicationRow row) =>
      viewModel.CanModerateCommunity && !viewModel.IsLoading &&
      viewModel.ApplicationRows.Any(current => current.Id == row.Id && current.Status == "pending");

}
