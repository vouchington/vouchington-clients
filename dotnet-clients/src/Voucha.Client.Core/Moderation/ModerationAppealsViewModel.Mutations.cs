using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  public async Task<bool> SaveDraftAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanEdit(appeal)) return false;
    var draft = DraftFor(appeal);
    if (string.Equals(draft, appeal.PublicResponse ?? string.Empty, StringComparison.Ordinal))
    {
      ConfirmServerDraft(appeal);
      return true;
    }
    return await MutateAsync(appeal.Id, async () =>
        Replace((await service.UpdatePublicResponseAsync(appeal.Id, draft, cancellationToken).ConfigureAwait(true)).Appeal), cancellationToken).ConfigureAwait(true);
  }

  public Task ApproveAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanApprove(appeal)) return Task.CompletedTask;
    return AwaitMutationAsync(appeal.Id, async () =>
    {
      var current = appeal;
      var draft = DraftFor(appeal);
      if (!string.Equals(draft, appeal.PublicResponse ?? string.Empty, StringComparison.Ordinal))
      {
        current = (await service.UpdatePublicResponseAsync(appeal.Id, draft, cancellationToken).ConfigureAwait(true)).Appeal;
        Replace(current);
      }
      Replace((await service.ApproveAsync(current.Id, cancellationToken).ConfigureAwait(true)).Appeal);
    }, cancellationToken);
  }

  public async Task DeliverAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanDeliver(appeal)) return;
    var delivered = await MutateAsync(appeal.Id, async () =>
    {
      Replace((await service.DeliverAsync(appeal.Id, cancellationToken).ConfigureAwait(true)).Appeal);
      ambiguousDeliveryIds.Remove(appeal.Id);
    }, cancellationToken, markAmbiguousDelivery: true).ConfigureAwait(true);
    if (!delivered && IsDeliveryAmbiguous(appeal))
    {
      await RefreshDeliveryStatusAsync(appeal, cancellationToken).ConfigureAwait(true);
    }
  }

  public Task ResolveAsync(ModerationAppeal appeal, ModerationAppealAction action, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanResolve(appeal, action)) return Task.CompletedTask;
    return AwaitMutationAsync(appeal.Id, async () =>
        Replace((await service.ResolveAsync(appeal.Id, action, cancellationToken).ConfigureAwait(true)).Appeal), cancellationToken);
  }

  public Task RerunAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanRerun(appeal)) return Task.CompletedTask;
    var revision = Volatile.Read(ref loadRevision);
    var ownerRevision = Volatile.Read(ref accountRevision);
    return AwaitMutationAsync(appeal.Id, async () =>
    {
      if (!CanAccess || ownerRevision != Volatile.Read(ref accountRevision)) return;
      cancellationToken.ThrowIfCancellationRequested();
      if (!TrySetPendingAIRerun(appeal, out var admission)) return;
      ModerationAppealQueueResponse queued;
      try
      {
        queued = await service.RerunResolutionDraftAsync(appeal.Id, cancellationToken).ConfigureAwait(true);
      }
      catch (Exception ex) when (!IsAmbiguous(ex))
      {
        ClearPendingAIRerun(appeal.Id, admission.Token);
        throw;
      }
      if (!queued.Queued)
      {
        ClearPendingAIRerun(appeal.Id, admission.Token);
        throw new InvalidOperationException(localization.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunNotQueued));
      }
      for (var attempt = 0; attempt < rerunPollAttempts; attempt++)
      {
        if (!IsCurrentAIRerunContext(appeal.Id, admission, revision, ownerRevision)) return;
        var refreshed = (await service.FetchAppealAsync(appeal.Id, cancellationToken).ConfigureAwait(true)).Appeal;
        if (!IsCurrentAIRerun(appeal.Id, admission, revision, ownerRevision, refreshed)) return;
        if (admission.IsReconciled(refreshed))
        {
          ClearPendingAIRerun(appeal.Id, admission.Token);
          Replace(refreshed, preserveUnconfirmedDirtyDraft: true);
          return;
        }
        if (attempt + 1 < rerunPollAttempts) await rerunPollDelay(cancellationToken).ConfigureAwait(true);
      }
      throw new TimeoutException(localization.Localize(
          UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing));
    }, cancellationToken);
  }

  private async Task<bool> MutateAsync(
      string id,
      Func<Task> operation,
      CancellationToken cancellationToken,
      bool markAmbiguousDelivery = false)
  {
    if (IsMutating) return false;
    var ownerRevision = Volatile.Read(ref accountRevision);
    activeMutationOwnerRevision = ownerRevision;
    activeMutationAdmissionToken = admissionStore.TryGet(admissionOwner, id, out var pending)
        ? pending.Token : null;
    MutatingAppealId = id;
    ErrorMessage = null;
    try
    {
      await operation().ConfigureAwait(true);
      return true;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex)
    {
      if (ownerRevision == Volatile.Read(ref accountRevision) && CanAccess)
      {
        if (markAmbiguousDelivery) ambiguousDeliveryIds.Add(id);
        ErrorMessage = ex.Message;
      }
      return false;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (ownerRevision == Volatile.Read(ref accountRevision) && CanAccess)
      {
        if (markAmbiguousDelivery && IsAmbiguous(ex)) ambiguousDeliveryIds.Add(id);
        ErrorMessage = ex.Message;
      }
      return false;
    }
    finally
    {
      activeMutationAdmissionToken = null;
      MutatingAppealId = null;
    }
  }

  private async Task AwaitMutationAsync(string id, Func<Task> operation, CancellationToken cancellationToken) =>
      _ = await MutateAsync(id, operation, cancellationToken).ConfigureAwait(true);

  private static bool IsAmbiguous(Exception exception) =>
      exception is OperationCanceledException ||
      exception is TimeoutException ||
      exception is VouchaApiDecodeException ||
      exception is HttpRequestException http &&
      (http.StatusCode is null || http.StatusCode == HttpStatusCode.RequestTimeout || http.StatusCode >= HttpStatusCode.InternalServerError);
}
