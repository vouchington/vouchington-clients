using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  private IReadOnlyList<ModerationDisputeSummary> summaries = [];
  public IReadOnlyList<ModerationDisputeSummary> Summaries
  {
    get => summaries;
    private set
    {
      if (!SetProperty(ref summaries, value)) return;
      OnPropertyChanged(nameof(HasDisputes));
      OnPropertyChanged(nameof(IsEmpty));
    }
  }

  public async Task<ModerationDispute> FetchDetailAsync(
      string id, CancellationToken cancellationToken = default)
  {
    if (!CanAccess) throw new InvalidOperationException("Staff access required.");
    var revision = Volatile.Read(ref loadRevision);
    var mutationRevision = Volatile.Read(ref mutationOutcomeRevision);
    var original = Disputes.FirstOrDefault(row => row.Id == id);
    var response = await service.FetchDisputeAsync(id, cancellationToken).ConfigureAwait(true);
    cancellationToken.ThrowIfCancellationRequested();
    if (!CanAccess || revision != Volatile.Read(ref loadRevision) ||
        mutationRevision != Volatile.Read(ref mutationOutcomeRevision) ||
        !ReferenceEquals(Disputes.FirstOrDefault(row => row.Id == id), original))
      throw new OperationCanceledException("Detail state changed.", cancellationToken);
    if (response.Dispute.Id != id) throw new InvalidOperationException("The detail response has a different identity.");
    Replace(response.Dispute, preserveUnconfirmedDirtyDraft: true);
    return response.Dispute;
  }

  private void UpdateSummary(ModerationDispute detail) =>
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
}
