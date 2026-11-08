using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel
{
  public async Task SubmitAsync(
      string? turnstileToken,
      CancellationToken cancellationToken = default)
  {
    if (IsSubmitting || ActiveTarget is null || viewer.IdentityId is null) return;
    var target = ActiveTarget;
    var draft = drafts.Get(viewer.IdentityId, target);
    var details = draft.Details.Trim();
    if (draft.Reason is null)
    {
      SubmissionOutcome = MemberAppealSubmissionOutcome.ReasonRequired;
      return;
    }
    if (details.Length == 0)
    {
      SubmissionOutcome = MemberAppealSubmissionOutcome.DetailsRequired;
      return;
    }
    if (details.Length > DetailsLimit)
    {
      SubmissionOutcome = MemberAppealSubmissionOutcome.DetailsTooLong;
      return;
    }

    IsSubmitting = true;
    SubmissionOutcome = MemberAppealSubmissionOutcome.Submitting;
    try
    {
      var response = await service.SubmitAsync(
          new ModerationAppealSubmissionRequest(
              target.TargetType,
              target.TargetId,
              draft.Reason.Value,
              details,
              target.PostRemovalKind,
              turnstileToken),
          cancellationToken).ConfigureAwait(true);
      cancellationToken.ThrowIfCancellationRequested();
      var pending = appealStates[ModerationAppealStatus.Pending];
      if (pending.Items.All(item => item.Id != response.Appeal.Id))
      {
        pending.Items.Insert(0, PublicSummary(response.Appeal));
      }
      LastSubmissionWasDuplicate = response.IsDuplicate;
      drafts.Clear(viewer.IdentityId, target);
      ActiveTarget = null;
      SubmissionOutcome = response.IsDuplicate
          ? MemberAppealSubmissionOutcome.Duplicate
          : MemberAppealSubmissionOutcome.Submitted;
      OnPropertyChanged(nameof(Appeals));
      OnTargetsChanged();
    }
    catch (OperationCanceledException)
    {
      if (Equals(ActiveTarget, target))
        SubmissionOutcome = MemberAppealSubmissionOutcome.Idle;
      throw;
    }
    catch (HttpRequestException)
    {
      SubmissionOutcome = MemberAppealSubmissionOutcome.Failed;
    }
    finally
    {
      IsSubmitting = false;
    }
  }

  public void SetSubmissionFailure() =>
      SubmissionOutcome = MemberAppealSubmissionOutcome.Failed;

  private static ModerationAppealSummary PublicSummary(ModerationAppeal appeal) =>
      new(appeal.Id, appeal.Status, RecommendedAction: null, appeal.IsOverdue,
          PublicResponse: appeal.SentAt is not null ? appeal.PublicResponse : null,
          appeal.ApprovedAt, appeal.SentAt, appeal.ResolvedAt, appeal.ResolutionAction,
          appeal.CreatedAt, appeal.UpdatedAt, HasAiDraft: null, HasDraft: null,
          AppellantUserId: null, appeal.UserWarningId, appeal.CommunityBanId, appeal.PostId,
          appeal.UserSuspensionId, appeal.CommunityId, appeal.PostRemovalKind,
          appeal.TargetContext, Appellant: null);

}
