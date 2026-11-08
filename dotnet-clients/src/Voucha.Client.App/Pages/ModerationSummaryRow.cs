using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

internal sealed class ModerationSummaryRow : VerticalStackLayout
{
  private readonly ModerationDisputesViewModel? disputes;
  private readonly ModerationAppealsViewModel? appeals;
  private readonly Func<Page, Task> open;

  public ModerationSummaryRow(ModerationDisputesViewModel viewModel,
      Func<Page, Task> open)
  {
    disputes = viewModel;
    this.open = open;
    BindingContextChanged += (_, _) => Populate();
    Loaded += (_, _) => Subscribe();
    Unloaded += (_, _) => Unsubscribe();
  }

  public ModerationSummaryRow(ModerationAppealsViewModel viewModel, Func<Page, Task> open)
  {
    appeals = viewModel;
    this.open = open;
    BindingContextChanged += (_, _) => Populate();
    Loaded += (_, _) => Subscribe();
    Unloaded += (_, _) => Unsubscribe();
  }

  private void Populate()
  {
    Children.Clear();
    Padding = 12;
    Spacing = 8;
    if (BindingContext is ModerationDisputeSummary dispute && disputes is { } disputeModel)
    {
      Children.Add(new Label { Text = Actor(dispute.Disputant), FontAttributes = FontAttributes.Bold });
      Children.Add(UserLabel(dispute.PostContent?.Text ?? dispute.PostLabel));
      if (!string.IsNullOrWhiteSpace(dispute.Reason)) Children.Add(UserLabel(dispute.Reason));
      AddCreated(dispute.CreatedAt);
      AddLifecycle(dispute.SentAt, dispute.ResolvedAt, dispute.IsOverdue);
      if (dispute.RecommendedAction is { } recommendation)
        Children.Add(UserLabel(ModerationDisputeCardPresentation.ActionLabel(recommendation)));
      AddState(dispute.Status.ToString(), dispute.HasDraft == true, dispute.HasAiDraft == true);
      if (dispute.SentAt is not null && !string.IsNullOrWhiteSpace(dispute.PublicResponse))
        Children.Add(UserLabel(dispute.PublicResponse));
      var button = OpenButton($"review-dispute-open-{dispute.Id}");
      button.Clicked += async (_, _) => await open(new ModerationDetailPage<ModerationDispute>(
          token => disputeModel.FetchDetailAsync(dispute.Id, token),
          (detail, token) => new ModerationDisputeCardView(disputeModel, () => token)
          { BindingContext = detail })).ConfigureAwait(true);
      Children.Add(button);
    }
    else if (BindingContext is ModerationAppealSummary appeal && appeals is { } appealModel)
    {
      Children.Add(new Label { Text = Actor(appeal.Appellant), FontAttributes = FontAttributes.Bold });
      if (appeal.TargetContext is ModerationAppealPostRemovalContext removal)
        Children.Add(UserLabel(removal.Title));
      AddTargetContext(appeal.TargetContext);
      AddCreated(appeal.CreatedAt);
      AddLifecycle(appeal.SentAt, appeal.ResolvedAt, appeal.IsOverdue);
      if (appeal.RecommendedAction is { } recommendation)
        Children.Add(UserLabel(UiCopy.Resolve(ModerationAppealCardPresentation.Action(recommendation))));
      AddState(appeal.Status.ToString(), appeal.HasDraft == true, appeal.HasAiDraft == true);
      if (appeal.SentAt is not null && !string.IsNullOrWhiteSpace(appeal.PublicResponse))
        Children.Add(UserLabel(appeal.PublicResponse));
      var button = OpenButton($"moderation-appeal-open-{appeal.Id}");
      button.Clicked += async (_, _) => await open(new ModerationDetailPage<ModerationAppeal>(
          token => appealModel.FetchDetailAsync(appeal.Id, token),
          (detail, token) => new ModerationAppealCardView(appealModel, () => token) { BindingContext = detail }))
          .ConfigureAwait(true);
      Children.Add(button);
    }
  }

  private void AddLifecycle(DateTimeOffset? sent, DateTimeOffset? resolved, bool? overdue)
  {
    if (sent is { } sentAt) Children.Add(UserLabel(ModerationAppealCardPresentation.LabelValue(
        UiMessageKey.NativeSwiftModerationAppealsSent, UiCopy.FormatDateTime(sentAt))));
    if (resolved is { } resolvedAt) Children.Add(UserLabel(ModerationAppealCardPresentation.LabelValue(
        UiMessageKey.NativeSwiftModerationAppealsResolved, UiCopy.FormatDateTime(resolvedAt))));
    if (overdue == true) Children.Add(UiCopy.Bind(new Label(), Label.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsOverdue));
  }

  private static Label UserLabel(string? value) =>
      new() { Text = UiCopy.Resolve(UiText.UserContent(value ?? string.Empty)) };

  private void AddCreated(DateTimeOffset? date)
  {
    if (date is not { } created) return;
    Children.Add(new Label
    {
      Text = ModerationAppealCardPresentation.LabelValue(
        UiMessageKey.NativeSwiftModerationAppealsCreated, UiCopy.FormatDateTime(created))
    });
  }

  private void AddTargetContext(ModerationAppealTargetContext? context)
  {
    var decision = context switch
    {
      ModerationAppealWarningContext warning => warning.PublicMessage,
      ModerationAppealCommunityBanContext ban => ban.Reason,
      ModerationAppealPostRemovalContext removal => removal.PublicReason,
      ModerationAppealSuspensionContext suspension => suspension.Reason,
      _ => null,
    };
    if (!string.IsNullOrWhiteSpace(decision)) Children.Add(UserLabel(decision));
  }

  private void Subscribe()
  {
    Unsubscribe();
    if (disputes is not null) disputes.PropertyChanged += Changed;
    if (appeals is not null) appeals.PropertyChanged += Changed;
    Populate();
  }

  private void Unsubscribe()
  {
    if (disputes is not null) disputes.PropertyChanged -= Changed;
    if (appeals is not null) appeals.PropertyChanged -= Changed;
  }

  private void Changed(object? sender, PropertyChangedEventArgs args) => Populate();

  private void AddState(string status, bool hasDraft, bool hasAiDraft)
  {
    var statusKey = status switch
    {
      "Resolved" => UiMessageKey.NativeSwiftModerationAppealsResolved,
      "Dismissed" => UiMessageKey.NativeSwiftModerationReportsDismissed,
      _ => UiMessageKey.NativeSwiftModerationReportsPending,
    };
    Children.Add(UiCopy.Bind(new Label(), Label.TextProperty, statusKey));
    if (hasDraft) Children.Add(UiCopy.Bind(new Label(), Label.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsDrafted));
    if (hasAiDraft) Children.Add(UiCopy.Bind(new Label(), Label.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsAiDrafted));
  }

  private static string Actor(ModerationActorSummary? actor) =>
      !string.IsNullOrWhiteSpace(actor?.VerifiedDisplayName) ? actor.VerifiedDisplayName :
      !string.IsNullOrWhiteSpace(actor?.Username) ? actor.Username :
      UiCopy.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDeletedUser);

  private static Button OpenButton(string id) => UiCopy.Bind(new Button { AutomationId = id },
      Button.TextProperty, UiMessageKey.NativeSwiftModerationReportsView);
}
