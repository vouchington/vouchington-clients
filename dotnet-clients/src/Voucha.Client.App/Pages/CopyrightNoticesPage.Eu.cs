using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CopyrightNoticesPage
{
  private void RenderEu(CopyrightEuParticipantCase eu)
  {
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesDecision));
    body.Children.Add(Message(eu.ReopenedAt is not null ? UiMessageKey.NativeCopyrightNoticesEuComplaintUpheldReviewing
        : eu.Outcome is null ? UiMessageKey.NativeCopyrightNoticesEuDecisionReviewing
        : eu.Outcome == "restrict" ? UiMessageKey.NativeCopyrightNoticesEuMaterialRestricted
        : UiMessageKey.NativeCopyrightNoticesEuNoAction));
    if (eu.DecidedAt is { } decided)
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesDecidedDate, ("date", UiCopy.FormatDateTime(decided))));
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesYourComplaint));
    body.Children.Add(Message(eu.Complaint.Request is not null ? UiMessageKey.NativeCopyrightNoticesComplaintReceived
        : eu.Outcome is null ? UiMessageKey.NativeCopyrightNoticesComplaintAfterDecision
        : eu.ReopenedAt is not null ? UiMessageKey.NativeCopyrightNoticesComplaintAfterRedecision
        : eu.Complaint.CanSubmit ? UiMessageKey.NativeCopyrightNoticesComplaintAvailable
        : UiMessageKey.NativeCopyrightNoticesComplaintCannotSubmit));
    if (eu.Complaint.WindowEndsAt is { } ends)
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesComplaintWindowEnds, ("date", UiCopy.FormatDateTime(ends))));
    if (eu.Complaint.Decision is { } decision)
    {
      body.Children.Add(Message(decision.StaffDisposition == "revoke" ? UiMessageKey.NativeCopyrightNoticesComplaintUpheld
          : UiMessageKey.NativeCopyrightNoticesComplaintMaintained));
      body.Children.Add(Value(decision.Rationale));
    }
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesOtherRedressRoutes));
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesEuOtherRedressRoutes));
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesDisputeSettlements));
    if (eu.DisputeSettlements.Count == 0)
      body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesNoDisputeSettlements));
    foreach (var settlement in eu.DisputeSettlements)
    {
      body.Children.Add(Value(settlement.BodyName));
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesReferredDate,
          ("date", UiCopy.FormatDateTime(settlement.ReferredAt))));
      if (settlement.Outcome is not { } outcome)
      {
        body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesNoSettlementOutcome));
        continue;
      }
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesSettlementOutcome,
          ("result", UiText.ProtocolValue(outcome.Result.Replace('_', ' '))),
          ("date", UiCopy.FormatDateTime(outcome.DecidedAt))));
      if (outcome.ImplementedAt is { } implemented)
        body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesSettlementImplemented,
            ("date", UiCopy.FormatDateTime(implemented))));
    }
    if (!eu.DisputeSettlementsPageInfo.HasNextPage) return;
    var more = UiCopy.Bind(new Button
    {
      AutomationId = "copyright-settlements-more", IsEnabled = !viewModel.IsLoadingSettlements,
    }, Button.TextProperty, viewModel.HasSettlementError ? UiMessageKey.NativeCommonRetry : UiMessageKey.NativeSwiftCommonLoadMore);
    more.Clicked += (_, _) => activeRequest = viewModel.LoadMoreSettlementsAsync(lifetime.Token);
    body.Children.Add(more);
  }
}
