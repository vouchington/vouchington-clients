using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityPenaltyReviewViewModel<TPenalty>
{
  public Task ConfirmAsync(string id, CancellationToken cancellationToken = default) =>
      MutateAsync(id, CanConfirm, ReviewExactAsync, true, cancellationToken);

  public Task RevokeAsync(string id, CancellationToken cancellationToken = default) =>
      MutateAsync(id, CanRevoke, RevokeExactAsync, false, cancellationToken);

  public async Task ReconcileAsync(string id)
  {
    if (!NeedsReconciliation(id) || !actionIds.Add(id)) return;
    Notify();
    try { await ReconcileCoreAsync(id).ConfigureAwait(true); }
    finally
    {
      actionIds.Remove(id);
      Notify();
    }
  }

  private async Task MutateAsync(
      string id,
      Func<string, bool> allowed,
      Func<string, CancellationToken, Task<TPenalty>> send,
      bool isReview,
      CancellationToken cancellationToken)
  {
    if (!allowed(id) || !actionIds.Add(id)) return;
    actionErrors.Remove(id);
    if (Find(id) is { } baseline) baselines[id] = baseline;
    Notify();
    try
    {
      Adopt(await send(id, cancellationToken).ConfigureAwait(true), isReview);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsAmbiguous(exception))
    {
      reconciliationIds.Add(id);
      actionErrors[id] = UiText.Localized(UiMessageKey.NativeSwiftIntegrityResultUncertain);
      await ReconcileCoreAsync(id).ConfigureAwait(true);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception))
    {
      actionErrors[id] = IsForbidden(exception)
          ? LosePermission()
          : UiText.ExternalContent(exception.Message);
    }
    finally
    {
      actionIds.Remove(id);
      if (!reconciliationIds.Contains(id)) baselines.Remove(id);
      Notify();
    }
  }

  /// <summary>
  /// Reads the authoritative penalty. A review is committed only when the server's review
  /// date moved, so an unchanged penalty may be retried without advancing the cadence twice.
  /// </summary>
  private async Task ReconcileCoreAsync(string id)
  {
    // The pre-mutation baseline survives queue refreshes that clear the visible rows.
    var before = baselines.TryGetValue(id, out var baseline) ? baseline : Find(id);
    try
    {
      var current = await FetchExactAsync(id, CancellationToken.None).ConfigureAwait(true);
      baselines.Remove(id);
      reconciliationIds.Remove(id);
      actionErrors.Remove(id);
      var advanced = before is null || ReviewDueAt(current) != ReviewDueAt(before) ||
          ReviewedAt(current) != ReviewedAt(before);
      if (IsRevoked(current) || advanced) Adopt(current, true);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception) ||
        exception is OperationCanceledException)
    {
      actionErrors[id] = IsForbidden(exception)
          ? LosePermission()
          : UiText.Localized(UiMessageKey.NativeSwiftIntegrityReconciliationFailed);
    }
  }

  private void Adopt(TPenalty penalty, bool committed)
  {
    var id = Id(penalty);
    if (IsRevoked(penalty))
    {
      Items = Items.Where(item => Id(item) != id).ToArray();
      committedIds.Remove(id);
      revokedIds.Add(id);
    }
    else
    {
      Items = Items.Select(item => Id(item) == id ? penalty : item).ToArray();
      if (committed) committedIds.Add(id);
    }
    Notify();
  }
}
