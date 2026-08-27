using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  public async Task<bool> SavePublicResponseAsync(
      ModerationDispute dispute,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanEdit(dispute)) return false;
    var draft = PublicResponseDraftFor(dispute);
    if (string.Equals(draft, dispute.PublicResponse ?? string.Empty, StringComparison.Ordinal))
    {
      dirtyPublicResponseIds.Remove(dispute.Id);
      return true;
    }
    return await MutateAsync(dispute.Id, async () =>
    {
      var response = await service.UpdatePublicResponseAsync(
          dispute.Id,
          draft,
          cancellationToken).ConfigureAwait(true);
      Replace(response.Dispute);
    }, cancellationToken).ConfigureAwait(true);
  }

  public Task ApproveAsync(
      ModerationDispute dispute,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanApprove(dispute)) return Task.CompletedTask;
    return AwaitMutationAsync(dispute.Id, async () =>
    {
      var current = dispute;
      var draft = PublicResponseDraftFor(dispute);
      if (!string.Equals(draft, dispute.PublicResponse ?? string.Empty, StringComparison.Ordinal))
      {
        current = (await service.UpdatePublicResponseAsync(
            dispute.Id,
            draft,
            cancellationToken).ConfigureAwait(true)).Dispute;
        Replace(current);
      }
      var approved = await service.ApproveAsync(
          current.Id,
          cancellationToken).ConfigureAwait(true);
      ambiguousMutationIds.Remove(dispute.Id);
      Replace(approved.Dispute);
    }, cancellationToken, marksAmbiguous: true);
  }

  public Task DeliverAsync(
      ModerationDispute dispute,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanDeliver(dispute)) return Task.CompletedTask;
    return AwaitMutationAsync(dispute.Id, async () =>
    {
      var delivered = await service.DeliverAsync(
          dispute.Id,
          cancellationToken).ConfigureAwait(true);
      ambiguousMutationIds.Remove(dispute.Id);
      Replace(delivered.Dispute);
    }, cancellationToken, marksAmbiguous: true);
  }

  public Task ResolveAsync(
      ModerationDispute dispute,
      ModerationDisputeResolutionAction action,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanResolve(dispute, action)) return Task.CompletedTask;
    var annotation = action == ModerationDisputeResolutionAction.Annotate
        ? AnnotationDraftFor(dispute).Trim()
        : null;
    return AwaitMutationAsync(dispute.Id, async () =>
    {
      var response = await service.ResolveAsync(
          dispute.Id,
          action,
          annotation,
          cancellationToken).ConfigureAwait(true);
      Replace(response.Dispute);
    }, cancellationToken, marksAmbiguous: true);
  }

  public Task RerunAsync(
      ModerationDispute dispute,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanRerun(dispute)) return Task.CompletedTask;
    return AwaitMutationAsync(dispute.Id, async () =>
    {
      LockPendingRerun(dispute);
      ModerationDisputeQueueResponse queued;
      try
      {
        queued = await service.RerunResolutionDraftAsync(
            dispute.Id,
            cancellationToken).ConfigureAwait(true);
      }
      catch (Exception ex) when (!IsAmbiguous(ex))
      {
        UnlockRerun(dispute.Id);
        throw;
      }
      if (!queued.Queued)
      {
        UnlockRerun(dispute.Id);
        throw new InvalidOperationException(localization.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunNotQueued));
      }
      for (var attempt = 0; attempt < rerunPollAttempts; attempt++)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var refreshed = (await service.FetchDisputeAsync(
            dispute.Id,
            cancellationToken).ConfigureAwait(true)).Dispute;
        if (HasRerunReconciled(refreshed))
        {
          UnlockRerun(dispute.Id);
          Replace(refreshed, preserveUnconfirmedDirtyDraft: true);
          return;
        }
        if (attempt + 1 < rerunPollAttempts)
          await rerunPollDelay(cancellationToken).ConfigureAwait(true);
      }
      throw new TimeoutException(localization.Localize(
          UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing));
    }, cancellationToken, marksAmbiguous: true);
  }

  private async Task<bool> MutateAsync(
      string id,
      Func<Task> operation,
      CancellationToken cancellationToken,
      bool marksAmbiguous = false)
  {
    if (IsMutating) return false;
    MutatingDisputeId = id;
    SetError(null);
    LastMutationError = null;
    try
    {
      await operation().ConfigureAwait(true);
      return true;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (marksAmbiguous) ambiguousMutationIds.Add(id);
      throw;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (marksAmbiguous && IsAmbiguous(ex)) ambiguousMutationIds.Add(id);
      LastMutationError = ex;
      SetError(PresentationErrorFor(ex));
      return false;
    }
    catch (OperationCanceledException ex)
    {
      if (marksAmbiguous) ambiguousMutationIds.Add(id);
      LastMutationError = ex;
      SetError(UiMessageKey.NativeSwiftModerationReportsActionFailed);
      return false;
    }
    finally
    {
      MutatingDisputeId = null;
    }
  }

  private async Task AwaitMutationAsync(
      string id,
      Func<Task> operation,
      CancellationToken cancellationToken,
      bool marksAmbiguous = false) =>
      _ = await MutateAsync(
          id,
          operation,
          cancellationToken,
          marksAmbiguous).ConfigureAwait(true);

}
