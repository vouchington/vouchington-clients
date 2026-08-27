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
        pending.Items.Insert(0, response.Appeal);
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
}
