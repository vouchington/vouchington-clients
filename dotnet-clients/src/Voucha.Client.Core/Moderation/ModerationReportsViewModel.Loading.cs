using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationReportsViewModel
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      IsQueueMutationRunning
          ? Task.CompletedTask
          : LoadPageAsync(append: false, allowClusterFallback: true, cancellationToken);

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      HasNextPage && !IsQueueInteractionBlocked
          ? LoadPageAsync(append: true, allowClusterFallback: false, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadPageAsync(bool append, bool allowClusterFallback, CancellationToken cancellationToken)
  {
    if (IsQueueInteractionBlocked) return;
    var requestGeneration = ++generation;
    if (loadCancellation is not null) await loadCancellation.CancelAsync().ConfigureAwait(true);
    loadCancellation?.Dispose();
    loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var token = loadCancellation.Token;
    State = LoadState.Loading;
    if (!append) ErrorMessage = null;
    try
    {
      if (!IsStaff)
      {
        await LoadMemberPageAsync(append, requestGeneration, token).ConfigureAwait(true);
      }
      else if (Mode == ModerationReportsMode.Grouped)
      {
        await LoadClusterPageAsync(append, requestGeneration, token).ConfigureAwait(true);
      }
      else
      {
        await LoadStaffPageAsync(append, requestGeneration, token).ConfigureAwait(true);
      }
      if (generation == requestGeneration) State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (generation != requestGeneration) return;
      if (!append && allowClusterFallback && IsStaff && Mode == ModerationReportsMode.Grouped)
      {
        mode = ModerationReportsMode.Flat;
        Notice = "Grouped reports could not load. Showing the report list instead.";
        OnPropertyChanged(nameof(Mode));
        State = LoadState.Idle;
        await LoadPageAsync(append: false, allowClusterFallback: false, cancellationToken).ConfigureAwait(true);
        return;
      }
      ErrorMessage = ex.Message;
      State = append ? LoadState.Loaded : LoadState.Error;
    }
  }

  private async Task LoadMemberPageAsync(bool append, int requestGeneration, CancellationToken token)
  {
    var response = await service.FetchMemberReportsAsync(
        StatusValue, "created_at_desc", append ? cursor : null, cancellationToken: token).ConfigureAwait(true);
    if (generation != requestGeneration) return;
    MemberReports = append ? Dedupe(MemberReports, response.Reports, item => item.Id) : response.Reports;
    ApplyPageInfo(response.PageInfo);
  }

  private async Task LoadStaffPageAsync(bool append, int requestGeneration, CancellationToken token)
  {
    var response = await service.FetchStaffReportsAsync(
        StatusValue, SortValue, append ? cursor : null, cancellationToken: token).ConfigureAwait(true);
    if (generation != requestGeneration) return;
    StaffReports = append ? Dedupe(StaffReports, response.Reports, item => item.Id) : response.Reports;
    if (!append) successfulReportIds.Clear();
    ApplyPageInfo(response.PageInfo);
  }

  private async Task LoadClusterPageAsync(bool append, int requestGeneration, CancellationToken token)
  {
    var response = await service.FetchClusteredReportsAsync(
        StatusValue, append ? cursor : null, cancellationToken: token).ConfigureAwait(true);
    if (generation != requestGeneration) return;
    Clusters = append ? Dedupe(Clusters, response.Clusters, item => item.Id) : response.Clusters;
    DuplicateClusters = append
        ? MergeDuplicateClusters(DuplicateClusters, response.DuplicateClusters)
        : response.DuplicateClusters;
    if (append)
      groupedPageCount++;
    else
    {
      groupedPageCount = 1;
      successfulReportIds.Clear();
    }
    ApplyPageInfo(response.PageInfo);
  }

  private void ApplyPageInfo(PageInfo pageInfo)
  {
    cursor = pageInfo.EndCursor;
    HasNextPage = pageInfo.HasNextPage;
    ErrorMessage = null;
  }

  private string StatusValue => Status switch
  {
    ModerationReportStatus.Pending => "pending",
    ModerationReportStatus.Reviewed => "reviewed",
    ModerationReportStatus.Actioned => "actioned",
    _ => "dismissed",
  };
  private string SortValue => Sort switch
  {
    ModerationReportSort.Severity => "severity",
    ModerationReportSort.MostReported => "most_reported",
    ModerationReportSort.CreatedAtAsc => "created_at_asc",
    _ => "created_at_desc",
  };

  private static T[] Dedupe<T>(IReadOnlyList<T> current, IReadOnlyList<T> next, Func<T, string> id) =>
      current.Concat(next).DistinctBy(id, StringComparer.Ordinal).ToArray();

  private static StaffModerationReportDuplicateCluster[] MergeDuplicateClusters(
      IReadOnlyList<StaffModerationReportDuplicateCluster> current,
      IReadOnlyList<StaffModerationReportDuplicateCluster> incoming)
  {
    var merged = current.ToList();
    var indexes = merged.Select((item, index) => (item.Id, index))
        .ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
    foreach (var duplicate in incoming)
    {
      if (!indexes.TryGetValue(duplicate.Id, out var index))
      {
        indexes[duplicate.Id] = merged.Count;
        merged.Add(duplicate);
        continue;
      }
      var existing = merged[index];
      var clusters = Dedupe(existing.Clusters, duplicate.Clusters, cluster => cluster.Id);
      var reasons = clusters.SelectMany(cluster => cluster.ReasonBreakdown)
          .GroupBy(item => item.Reason, StringComparer.Ordinal)
          .Select(group => new ModerationReportReasonBreakdown(group.Key, group.Sum(item => item.Count)))
          .ToArray();
      var firstReportedAt = clusters.Length == 0
          ? (existing.FirstReportedAt <= duplicate.FirstReportedAt ? existing.FirstReportedAt : duplicate.FirstReportedAt)
          : clusters.Min(cluster => cluster.FirstReportedAt);
      var lastReportedAt = clusters.Length == 0
          ? (existing.LastReportedAt >= duplicate.LastReportedAt ? existing.LastReportedAt : duplicate.LastReportedAt)
          : clusters.Max(cluster => cluster.LastReportedAt);
      merged[index] = existing with
      {
        PostCount = clusters.Length,
        ReportCount = clusters.Sum(cluster => cluster.ReportCount),
        ReasonBreakdown = reasons,
        FirstReportedAt = firstReportedAt,
        LastReportedAt = lastReportedAt,
        Clusters = clusters,
      };
    }
    return [.. merged];
  }
}
