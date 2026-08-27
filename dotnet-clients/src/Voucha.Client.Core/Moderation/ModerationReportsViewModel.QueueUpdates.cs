using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationReportsViewModel
{
  public StaffModerationReport? FindStaffReport(string reportId) =>
      successfulReportIds.Contains(reportId)
          ? null
          : AllStaffReports().FirstOrDefault(report => report.Id == reportId);

  public IReadOnlyList<StaffModerationReport> ReportsForCluster(string clusterId)
  {
    var entityCluster = Clusters.FirstOrDefault(cluster => cluster.Id == clusterId);
    if (entityCluster is not null) return ReportsForCluster(entityCluster);
    return DuplicateClusters.FirstOrDefault(cluster => cluster.Id == clusterId)?.Clusters
        .SelectMany(ReportsForCluster)
        .DistinctBy(report => report.Id, StringComparer.Ordinal).ToArray() ?? [];
  }

  public IReadOnlyList<StaffModerationReport> ReportsForCluster(
      StaffModerationReportEntityCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    return cluster.Reports.Where(report => !successfulReportIds.Contains(report.Id)).ToArray();
  }

  public bool CanDismissCluster(string clusterId) => ReportsForCluster(clusterId).Any(CanResolve);

  public bool CanRemoveClusterTargets(string clusterId) => ReportsForCluster(clusterId).Any(CanRemove);

  public bool HasActiveReports(StaffModerationReportDuplicateCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    return cluster.Clusters.Any(item => ReportsForCluster(item).Count > 0);
  }

  public int PresentationReportCount(StaffModerationReportEntityCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    var activeCount = ReportsForCluster(cluster).Count;
    return activeCount == cluster.Reports.Count ? cluster.ReportCount : activeCount;
  }

  public (int Posts, int Reports) PresentationCounts(StaffModerationReportDuplicateCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    var activeClusters = cluster.Clusters.Where(item => ReportsForCluster(item).Count > 0).ToArray();
    var activeCount = activeClusters.Sum(item => ReportsForCluster(item).Count);
    var loadedCount = cluster.Clusters.Sum(item => item.Reports.Count);
    return loadedCount == activeCount
        ? (cluster.PostCount, cluster.ReportCount)
        : (activeClusters.Length, activeCount);
  }

  public IReadOnlyList<ModerationReportReasonBreakdown> PresentationReasons(
      StaffModerationReportEntityCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    var active = ReportsForCluster(cluster);
    return active.Count == cluster.Reports.Count ? cluster.ReasonBreakdown : ReasonsFor(active);
  }

  public IReadOnlyList<ModerationReportReasonBreakdown> PresentationReasons(
      StaffModerationReportDuplicateCluster cluster)
  {
    ArgumentNullException.ThrowIfNull(cluster);
    var loaded = cluster.Clusters.SelectMany(item => item.Reports).ToArray();
    var active = cluster.Clusters.SelectMany(ReportsForCluster).ToArray();
    return active.Length == loaded.Length ? cluster.ReasonBreakdown : ReasonsFor(active);
  }

  private static ModerationReportReasonBreakdown[] ReasonsFor(
      IEnumerable<StaffModerationReport> reports) =>
      reports.GroupBy(report => report.Reason, StringComparer.Ordinal)
          .Select(group => new ModerationReportReasonBreakdown(group.Key, group.Count())).ToArray();

  private StaffModerationReport[] AllStaffReports() =>
      StaffReports
          .Concat(Clusters.SelectMany(cluster => cluster.Reports))
          .Concat(DuplicateClusters.SelectMany(duplicate => duplicate.Clusters).SelectMany(cluster => cluster.Reports))
          .Where(report => !successfulReportIds.Contains(report.Id))
          .DistinctBy(report => report.Id, StringComparer.Ordinal)
          .ToArray();

  private async Task ApplySuccessfulMutationAsync(
      IEnumerable<string> reportIds,
      CancellationToken cancellationToken)
  {
    var ids = reportIds.ToHashSet(StringComparer.Ordinal);
    if (ids.Count == 0) return;
    successfulReportIds.UnionWith(ids);
    foreach (var id in ids)
    {
      selectedIds.Remove(id);
      rowErrors.Remove(id);
    }
    OnPropertyChanged(nameof(SelectedIds));
    OnPropertyChanged(nameof(RowErrors));
    if (Mode != ModerationReportsMode.Grouped)
    {
      StaffReports = StaffReports.Where(report => !ids.Contains(report.Id)).ToArray();
      return;
    }
    await RefreshAcceptedGroupedPagesAsync(
        Math.Max(groupedPageCount, 1), cancellationToken).ConfigureAwait(true);
  }

  private async Task RefreshAcceptedGroupedPagesAsync(
      int pageCount,
      CancellationToken cancellationToken)
  {
    var requestGeneration = ++generation;
    if (loadCancellation is not null) await loadCancellation.CancelAsync().ConfigureAwait(true);
    loadCancellation?.Dispose();
    loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var token = loadCancellation.Token;
    State = LoadState.Loading;
    try
    {
      IReadOnlyList<StaffModerationReportEntityCluster> refreshedClusters = [];
      IReadOnlyList<StaffModerationReportDuplicateCluster> refreshedDuplicates = [];
      PageInfo? finalPageInfo = null;
      string? nextCursor = null;
      var acceptedPages = 0;
      for (var page = 0; page < pageCount; page++)
      {
        var response = await service.FetchClusteredReportsAsync(
            StatusValue, nextCursor, cancellationToken: token).ConfigureAwait(true);
        refreshedClusters = Dedupe(refreshedClusters, response.Clusters, cluster => cluster.Id);
        refreshedDuplicates = MergeDuplicateClusters(refreshedDuplicates, response.DuplicateClusters);
        finalPageInfo = response.PageInfo;
        nextCursor = response.PageInfo.EndCursor;
        acceptedPages++;
        if (!response.PageInfo.HasNextPage) break;
      }
      if (generation != requestGeneration || finalPageInfo is null) return;
      Clusters = refreshedClusters;
      DuplicateClusters = refreshedDuplicates;
      groupedPageCount = acceptedPages;
      successfulReportIds.Clear();
      ApplyPageInfo(finalPageInfo);
      Notice = null;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (generation != requestGeneration) return;
      State = LoadState.Loaded;
      Notice = "Reports changed. Refresh the queue to see current totals.";
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (generation != requestGeneration) return;
      State = LoadState.Loaded;
      ErrorMessage = ex.Message;
      Notice = "Reports changed. Refresh the queue to see current totals.";
    }
  }
}
