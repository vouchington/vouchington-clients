using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  private IReadOnlyList<ModerationAppealSummary> summaries = [];
  public IReadOnlyList<ModerationAppealSummary> Summaries
  {
    get => summaries;
    private set
    {
      if (!SetProperty(ref summaries, value)) return;
      OnPropertyChanged(nameof(HasAppeals));
      OnPropertyChanged(nameof(IsEmpty));
    }
  }

  private readonly Dictionary<(int OwnerRevision, string Id), PendingDetailRead> pendingDetailReads = [];

  public async Task<ModerationAppeal> FetchDetailAsync(string id, CancellationToken cancellationToken = default)
  {
    if (!CanAccess) throw new InvalidOperationException("Staff access required.");
    var ownerRevision = Volatile.Read(ref accountRevision);
    var key = (ownerRevision, id);
    if (pendingDetailReads.TryGetValue(key, out var pending) &&
        !pending.OwnerToken.IsCancellationRequested && !pending.Read.IsCompleted)
      return await pending.Read.WaitAsync(cancellationToken).ConfigureAwait(true);
    var read = FetchDetailCoreAsync(id, ownerRevision, cancellationToken);
    var entry = new PendingDetailRead(read, cancellationToken);
    pendingDetailReads[key] = entry;
    try { return await read.ConfigureAwait(true); }
    finally
    {
      if (pendingDetailReads.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
        pendingDetailReads.Remove(key);
    }
  }

  private async Task<ModerationAppeal> FetchDetailCoreAsync(
      string id, int ownerRevision, CancellationToken cancellationToken)
  {
    var revision = Volatile.Read(ref loadRevision);
    var original = Appeals.FirstOrDefault(row => row.Id == id);
    var response = await service.FetchAppealAsync(id, cancellationToken).ConfigureAwait(true);
    cancellationToken.ThrowIfCancellationRequested();
    if (!CanAccess || IsMutating || ownerRevision != Volatile.Read(ref accountRevision) ||
        revision != Volatile.Read(ref loadRevision) ||
        !ReferenceEquals(Appeals.FirstOrDefault(row => row.Id == id), original))
      throw new OperationCanceledException("Detail owner or state changed.", cancellationToken);
    if (response.Appeal.Id != id) throw new InvalidOperationException("The detail response has a different identity.");
    ReconcileAIReruns([response.Appeal]);
    Replace(response.Appeal, preserveUnconfirmedDirtyDraft: true, readOwnerRevision: ownerRevision);
    return response.Appeal;
  }

  private void UpdateSummary(ModerationAppeal detail) =>
      Summaries = Summaries.Where(row => row.Id != detail.Id || detail.Status == SelectedStatus)
          .Select(row => row.Id != detail.Id ? row : row with
          {
            Status = detail.Status,
            HasDraft = detail.PublicResponse is not null,
            PublicResponse = detail.SentAt is not null ? detail.PublicResponse : null,
            ApprovedAt = detail.ApprovedAt,
            SentAt = detail.SentAt,
            ResolvedAt = detail.ResolvedAt,
            ResolutionAction = detail.ResolutionAction,
            HasAiDraft = detail.AiDraftedAt is not null,
            RecommendedAction = detail.RecommendedAction,
          }).ToArray();

  private sealed record PendingDetailRead(Task<ModerationAppeal> Read, CancellationToken OwnerToken);

}
