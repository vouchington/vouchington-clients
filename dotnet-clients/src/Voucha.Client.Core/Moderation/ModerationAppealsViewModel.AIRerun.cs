using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  private readonly Dictionary<string, AIRerunBaseline> pendingAIReruns =
      new(StringComparer.Ordinal);

  public bool IsAIRerunPending(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    lock (pendingAIReruns) return pendingAIReruns.ContainsKey(appeal.Id);
  }

  public bool CanRefreshAIRerun(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return CanAccess && !IsMutating && appeal.Status == ModerationAppealStatus.Pending &&
        Appeals.Any(item => item.Id == appeal.Id) && IsAIRerunPending(appeal);
  }

  public Task RefreshPendingAIRerunAsync(
      ModerationAppeal appeal,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanRefreshAIRerun(appeal) || !TryGetPendingAIRerun(appeal.Id, out var baseline))
      return Task.CompletedTask;
    var revision = Volatile.Read(ref loadRevision);
    var ownerRevision = Volatile.Read(ref accountRevision);
    return AwaitMutationAsync(appeal.Id, async () =>
    {
      var refreshed = (await service.FetchAppealAsync(appeal.Id, cancellationToken)
          .ConfigureAwait(true)).Appeal;
      if (!IsCurrentAIRerun(appeal.Id, baseline, revision, ownerRevision, refreshed)) return;
      if (!baseline.IsReconciled(refreshed))
        throw new TimeoutException(localization.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing));
      ClearPendingAIRerun(appeal.Id);
      Replace(refreshed, preserveUnconfirmedDirtyDraft: true);
    }, cancellationToken);
  }

  private bool IsCurrentAIRerun(
      string appealId,
      AIRerunBaseline baseline,
      int revision,
      int ownerRevision,
      ModerationAppeal refreshed) =>
      refreshed.Id == appealId &&
      IsCurrentAIRerunContext(appealId, baseline, revision, ownerRevision);

  private bool IsCurrentAIRerunContext(
      string appealId,
      AIRerunBaseline baseline,
      int revision,
      int ownerRevision) =>
      CanAccess && ownerRevision == Volatile.Read(ref accountRevision) &&
      revision == Volatile.Read(ref loadRevision) &&
      SelectedStatus == ModerationAppealStatus.Pending &&
      Appeals.Any(item => item.Id == appealId) &&
      TryGetPendingAIRerun(appealId, out var current) && current == baseline;

  private void ReconcileAIReruns(IEnumerable<ModerationAppeal> loaded)
  {
    foreach (var appeal in loaded)
    {
      if (TryGetPendingAIRerun(appeal.Id, out var baseline) && baseline.IsReconciled(appeal))
        ClearPendingAIRerun(appeal.Id);
    }
  }

  private void SetPendingAIRerun(ModerationAppeal appeal)
  {
    lock (pendingAIReruns)
      pendingAIReruns[appeal.Id] = new(appeal.AiDraftedAt, appeal.LatestLifecycleChangeId);
    OnPropertyChanged(nameof(Appeals));
  }

  private void ClearPendingAIRerun(string appealId)
  {
    lock (pendingAIReruns) pendingAIReruns.Remove(appealId);
    OnPropertyChanged(nameof(Appeals));
  }

  private bool TryGetPendingAIRerun(string appealId, out AIRerunBaseline baseline)
  {
    lock (pendingAIReruns) return pendingAIReruns.TryGetValue(appealId, out baseline!);
  }

  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args)
  {
    if (viewer.HasSameIdentityAccess(args.Viewer)) return;
    _ = Interlocked.Increment(ref accountRevision);
    _ = Interlocked.Increment(ref loadRevision);
    lock (pendingAIReruns) pendingAIReruns.Clear();
    drafts.Clear();
    dirtyDraftIds.Clear();
    ambiguousDeliveryIds.Clear();
    Appeals = [];
    State = LoadState.Idle;
    ErrorMessage = null;
    OnPropertyChanged(nameof(Appeals));
  }

  private sealed record AIRerunBaseline(
      DateTimeOffset? AiDraftedAt,
      string? LifecycleChangeId)
  {
    public bool IsReconciled(ModerationAppeal appeal) =>
        appeal.AiDraftedAt is not null &&
        appeal.AiDraftedAt != AiDraftedAt &&
        !string.Equals(appeal.LatestLifecycleChangeId, LifecycleChangeId, StringComparison.Ordinal) ||
        appeal.ApprovedAt is not null || appeal.SentAt is not null ||
        appeal.Status != ModerationAppealStatus.Pending;
  }
}
