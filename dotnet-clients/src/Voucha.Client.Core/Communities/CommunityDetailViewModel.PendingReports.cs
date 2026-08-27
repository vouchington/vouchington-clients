using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private readonly CursorPaginationState<CommunityPendingReport, string> pendingReportPages =
      new(report => report.Id);

  public IReadOnlyList<CommunityModerationRow> PendingReportRows =>
      pendingReportPages.Items.Select(report => new CommunityModerationRow(
          report.Id,
          report.TargetLabel ?? report.EntityId ?? report.Id,
          report.Status,
          report.Reason)).ToArray();

  public bool HasMorePendingReports => pendingReportPages.HasMore;
  public bool IsLoadingMorePendingReports => pendingReportPages.IsLoading;
  public bool CanAutomaticallyLoadPendingReports =>
      SelectedSection == CommunityDetailSurfaceSection.Moderation &&
      pendingReportPages.CanAutomaticallyLoad;
  public bool HasPendingReportPaginationError => pendingReportPages.LastError is not null;
  public string? PendingReportPaginationErrorMessage => pendingReportPages.LastError;

  public async Task LoadPendingReportsAsync(CancellationToken cancellationToken = default)
  {
    pendingReportPages.Reset();
    await LoadPendingReportPageAsync(propagateFailure: true, cancellationToken).ConfigureAwait(true);
  }

  public Task LoadMorePendingReportsAsync(CancellationToken cancellationToken = default) =>
      LoadPendingReportPageAsync(propagateFailure: false, cancellationToken);

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Each request owns its retry state; initial loads rethrow and continuations expose it.")]
  private async Task LoadPendingReportPageAsync(
      bool propagateFailure,
      CancellationToken cancellationToken)
  {
    var request = pendingReportPages.BeginNextPage();
    if (request is null) return;
    var community = communityIdOrSlug;
    try
    {
      var response = await service.FetchPendingReportsPageAsync(
          community,
          request.Cursor,
          20,
          cancellationToken).ConfigureAwait(true);
      if (!string.Equals(communityIdOrSlug, community, StringComparison.Ordinal) ||
          SelectedSection != CommunityDetailSurfaceSection.Moderation)
      {
        pendingReportPages.Cancel(request);
        NotifyPendingReportPagination();
        return;
      }
      pendingReportPages.Complete(
          request,
          response.Reports,
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage);
      NotifyPendingReportPagination();
    }
    catch (OperationCanceledException) when (propagateFailure || cancellationToken.IsCancellationRequested)
    {
      pendingReportPages.Cancel(request);
      NotifyPendingReportPagination();
      if (propagateFailure) throw;
    }
    catch (Exception ex)
    {
      pendingReportPages.Fail(request, ex.Message);
      NotifyPendingReportPagination();
      if (propagateFailure) throw;
    }
  }

  private void TombstoneResolvedReport(string reportId)
  {
    pendingReportPages.Remove(report => string.Equals(report.Id, reportId, StringComparison.Ordinal));
    ModerationRows = ModerationRows
        .Where(report => !string.Equals(report.Id, reportId, StringComparison.Ordinal))
        .ToArray();
    Moderation = Moderation
        .Where(report => !string.Equals(report.Id, reportId, StringComparison.Ordinal))
        .ToArray();
    OnPropertyChanged(nameof(ModerationRows));
    OnPropertyChanged(nameof(Moderation));
    NotifyPendingReportPagination();
  }

  private void InvalidatePendingReportRequests()
  {
    pendingReportPages.Reset();
    NotifyPendingReportPagination();
  }

  private void NotifyPendingReportPagination()
  {
    OnPropertyChanged(nameof(PendingReportRows));
    OnPropertyChanged(nameof(HasMorePendingReports));
    OnPropertyChanged(nameof(IsLoadingMorePendingReports));
    OnPropertyChanged(nameof(CanAutomaticallyLoadPendingReports));
    OnPropertyChanged(nameof(HasPendingReportPaginationError));
    OnPropertyChanged(nameof(PendingReportPaginationErrorMessage));
  }
}
