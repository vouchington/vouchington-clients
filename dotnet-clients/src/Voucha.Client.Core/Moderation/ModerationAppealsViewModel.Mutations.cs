using System.Net;
using Voucha.Client.Core.Api;

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
    return AwaitMutationAsync(appeal.Id, async () =>
    {
      var queued = await service.RerunResolutionDraftAsync(appeal.Id, cancellationToken).ConfigureAwait(true);
      if (!queued.Queued) throw new InvalidOperationException("The AI draft was not queued.");
      for (var attempt = 0; attempt < rerunPollAttempts; attempt++)
      {
        var refreshed = (await service.FetchAppealAsync(appeal.Id, cancellationToken).ConfigureAwait(true)).Appeal;
        if (refreshed.AiDraftedAt is not null &&
            refreshed.AiDraftedAt != appeal.AiDraftedAt &&
            !string.Equals(refreshed.LatestLifecycleChangeId, appeal.LatestLifecycleChangeId, StringComparison.Ordinal))
        {
          Replace(refreshed);
          return;
        }
        if (attempt + 1 < rerunPollAttempts) await rerunPollDelay(cancellationToken).ConfigureAwait(true);
      }
      throw new TimeoutException("The AI draft is still processing. Refresh to check again.");
    }, cancellationToken);
  }

  private async Task<bool> MutateAsync(
      string id,
      Func<Task> operation,
      CancellationToken cancellationToken,
      bool markAmbiguousDelivery = false)
  {
    if (IsMutating) return false;
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
      if (markAmbiguousDelivery) ambiguousDeliveryIds.Add(id);
      ErrorMessage = ex.Message;
      return false;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (markAmbiguousDelivery && IsAmbiguous(ex)) ambiguousDeliveryIds.Add(id);
      ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      MutatingAppealId = null;
    }
  }

  private async Task AwaitMutationAsync(string id, Func<Task> operation, CancellationToken cancellationToken) =>
      _ = await MutateAsync(id, operation, cancellationToken).ConfigureAwait(true);

  private void SeedDrafts(IEnumerable<ModerationAppeal> loaded)
  {
    foreach (var appeal in loaded)
    {
      if (!dirtyDraftIds.Contains(appeal.Id)) drafts[appeal.Id] = ServerDraftFor(appeal);
    }
  }

  private void Replace(ModerationAppeal appeal)
  {
    ConfirmServerDraft(appeal);
    var updated = Appeals.ToList();
    var index = updated.FindIndex(item => item.Id == appeal.Id);
    if (appeal.Status != SelectedStatus)
    {
      if (index >= 0) updated.RemoveAt(index);
    }
    else if (index >= 0)
    {
      updated[index] = appeal;
    }
    else
    {
      updated.Insert(0, appeal);
    }
    Appeals = updated;
  }

  private void ConfirmServerDraft(ModerationAppeal appeal)
  {
    drafts[appeal.Id] = ServerDraftFor(appeal);
    dirtyDraftIds.Remove(appeal.Id);
  }

  private static bool IsAmbiguous(Exception exception) =>
      exception is OperationCanceledException ||
      exception is HttpRequestException http &&
      (http.StatusCode is null || http.StatusCode == HttpStatusCode.RequestTimeout || http.StatusCode >= HttpStatusCode.InternalServerError);
}
