using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public interface IIntegrityPenaltyReviewService
{
  Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltyReviewsDueAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<ReportAbusePenaltyResponse> ReviewReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);

  Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltyReviewsDueAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<VoteWeightPenaltyResponse> ReviewVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);
}

public interface IIntegrityPenaltyReviewModel : INotifyPropertyChanged
{
  bool IsAuthorized { get; }
  LoadState State { get; }
  string? ErrorMessage { get; }
  bool IsEmpty { get; }
  bool HasMore { get; }
  bool CanLoadMore { get; }
  IReadOnlyList<IntegrityPenaltyRow> Rows { get; }
  bool CanConfirm(string id);
  bool CanRevoke(string id);
  bool NeedsReconciliation(string id);
  bool IsCommitted(string id);
  string? ActionError(string id);
  Task LoadAsync(CancellationToken cancellationToken = default);
  Task RefreshAsync(CancellationToken cancellationToken = default);
  Task LoadMoreAsync(CancellationToken cancellationToken = default);
  Task ConfirmAsync(string id, CancellationToken cancellationToken = default);
  Task RevokeAsync(string id, CancellationToken cancellationToken = default);
  Task ReconcileAsync(string id);
}
