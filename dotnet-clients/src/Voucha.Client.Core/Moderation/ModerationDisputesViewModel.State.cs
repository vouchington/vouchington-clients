using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  public const int AnnotationBodyMaximumLength = 2000;

  private readonly Dictionary<string, string> publicResponseDrafts =
      new(StringComparer.Ordinal);
  private readonly Dictionary<string, string> annotationDrafts =
      new(StringComparer.Ordinal);
  private readonly HashSet<string> dirtyPublicResponseIds =
      new(StringComparer.Ordinal);
  private readonly HashSet<string> ambiguousMutationIds =
      new(StringComparer.Ordinal);
  private readonly Dictionary<string, RerunBaseline> rerunBaselines =
      new(StringComparer.Ordinal);

  public string PublicResponseDraftFor(ModerationDispute dispute)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    return publicResponseDrafts.TryGetValue(dispute.Id, out var value)
        ? value
        : PublicResponseSeed(dispute);
  }

  public string AnnotationDraftFor(ModerationDispute dispute)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    return annotationDrafts.GetValueOrDefault(dispute.Id, string.Empty);
  }

  public void SetPublicResponseDraft(ModerationDispute dispute, string value)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    var draft = value ?? string.Empty;
    publicResponseDrafts[dispute.Id] = draft;
    if (string.Equals(draft, PublicResponseSeed(dispute), StringComparison.Ordinal))
      dirtyPublicResponseIds.Remove(dispute.Id);
    else
      dirtyPublicResponseIds.Add(dispute.Id);
    OnPropertyChanged(nameof(Disputes));
  }

  public void SetAnnotationDraft(ModerationDispute dispute, string value)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    annotationDrafts[dispute.Id] = value ?? string.Empty;
    OnPropertyChanged(nameof(Disputes));
  }

  public bool IsMutationAmbiguous(ModerationDispute dispute)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    return ambiguousMutationIds.Contains(dispute.Id);
  }

  public bool CanEdit(ModerationDispute dispute) =>
      CanMutate(dispute) && dispute.SentAt is null;

  public bool CanApprove(ModerationDispute dispute) =>
      CanMutate(dispute) &&
      dispute.ApprovedAt is null &&
      dispute.SentAt is null &&
      !string.IsNullOrWhiteSpace(PublicResponseDraftFor(dispute));

  public bool CanDeliver(ModerationDispute dispute) =>
      CanMutate(dispute) &&
      dispute.ApprovedAt is not null &&
      dispute.SentAt is null &&
      string.Equals(
          PublicResponseDraftFor(dispute),
          dispute.PublicResponse ?? string.Empty,
          StringComparison.Ordinal);

  public bool CanRerun(ModerationDispute dispute) =>
      CanMutate(dispute) && dispute.ApprovedAt is null && dispute.SentAt is null;

  public bool CanEditAnnotation(ModerationDispute dispute) =>
      CanMutate(dispute) && dispute.SentAt is not null;

  public bool CanResolve(
      ModerationDispute dispute,
      ModerationDisputeResolutionAction action)
  {
    if (!CanEditAnnotation(dispute)) return false;
    if (action != ModerationDisputeResolutionAction.Annotate) return true;
    var annotation = AnnotationDraftFor(dispute).Trim();
    return annotation.Length is > 0 and <= AnnotationBodyMaximumLength;
  }

  private bool CanMutate(ModerationDispute dispute)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    return CanAccess &&
        dispute.Status == ModerationDisputeStatus.Pending &&
        !IsMutating &&
        !ambiguousMutationIds.Contains(dispute.Id);
  }

  private void SeedDrafts(IEnumerable<ModerationDispute> loaded)
  {
    foreach (var dispute in loaded)
    {
      if (!dirtyPublicResponseIds.Contains(dispute.Id))
        publicResponseDrafts[dispute.Id] = PublicResponseSeed(dispute);
      annotationDrafts.TryAdd(dispute.Id, string.Empty);
    }
  }

  private void Replace(
      ModerationDispute dispute,
      bool preserveUnconfirmedDirtyDraft = false)
  {
    _ = Interlocked.Increment(ref mutationOutcomeRevision);
    if (State == LoadState.Loading) State = LoadState.Loaded;
    var preserveDraft =
        preserveUnconfirmedDirtyDraft &&
        dirtyPublicResponseIds.Contains(dispute.Id) &&
        !string.Equals(
            PublicResponseDraftFor(dispute),
            dispute.PublicResponse ?? string.Empty,
            StringComparison.Ordinal);
    if (!preserveDraft)
    {
      publicResponseDrafts[dispute.Id] = PublicResponseSeed(dispute);
      dirtyPublicResponseIds.Remove(dispute.Id);
    }
    annotationDrafts.TryAdd(dispute.Id, string.Empty);
    var updated = Disputes.ToList();
    var index = updated.FindIndex(item => item.Id == dispute.Id);
    if (dispute.Status != SelectedStatus)
    {
      if (index >= 0) updated.RemoveAt(index);
    }
    else if (index >= 0)
      updated[index] = dispute;
    else
      updated.Insert(0, dispute);
    Disputes = updated;
  }

  private static string PublicResponseSeed(ModerationDispute dispute) =>
      dispute.PublicResponse ?? dispute.AiPublicResponse ?? string.Empty;

  private void LockPendingRerun(ModerationDispute dispute)
  {
    rerunBaselines[dispute.Id] =
        new(dispute.AiDraftedAt, dispute.LatestLifecycleChangeId);
    ambiguousMutationIds.Add(dispute.Id);
  }

  private void UnlockRerun(string disputeId)
  {
    rerunBaselines.Remove(disputeId);
    ambiguousMutationIds.Remove(disputeId);
  }

  private bool HasRerunAdvanced(ModerationDispute dispute) =>
      !rerunBaselines.TryGetValue(dispute.Id, out var baseline) ||
      dispute.AiDraftedAt is not null &&
      dispute.AiDraftedAt != baseline.AiDraftedAt &&
      !string.Equals(
          dispute.LatestLifecycleChangeId,
          baseline.LatestLifecycleChangeId,
          StringComparison.Ordinal);

  private bool HasRerunReconciled(ModerationDispute dispute) =>
      HasRerunAdvanced(dispute) ||
      dispute.ApprovedAt is not null ||
      dispute.SentAt is not null ||
      dispute.Status != ModerationDisputeStatus.Pending;

  private sealed record RerunBaseline(
      DateTimeOffset? AiDraftedAt,
      string? LatestLifecycleChangeId);
}
