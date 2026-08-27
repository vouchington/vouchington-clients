using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed record ModerationBulkResult(int Succeeded, int Failed, int Skipped);

public sealed partial class ModerationReportsViewModel
{
  public Task<ModerationBulkResult> DismissSelectedAsync(CancellationToken cancellationToken = default) =>
      DismissReportsAsync(selectedIds.ToArray(), cancellationToken);

  public Task<ModerationBulkResult> RemoveSelectedTargetsAsync(CancellationToken cancellationToken = default) =>
      RemoveTargetsAsync(selectedIds.ToArray(), cancellationToken);

  public async Task<ModerationBulkResult> DismissClusterAsync(
      string clusterId,
      CancellationToken cancellationToken = default)
  {
    var reports = ReportsForCluster(clusterId);
    if (IsQueueInteractionBlocked) return new(0, 0, reports.Count);
    var ids = SelectLoadedReports(reports);
    return await DismissReportsAsync(ids, cancellationToken).ConfigureAwait(true);
  }

  public async Task<ModerationBulkResult> RemoveClusterTargetsAsync(
      string clusterId,
      CancellationToken cancellationToken = default)
  {
    var reports = ReportsForCluster(clusterId);
    if (IsQueueInteractionBlocked) return new(0, 0, reports.Count);
    var ids = SelectLoadedReports(reports);
    return await RemoveTargetsAsync(ids, cancellationToken).ConfigureAwait(true);
  }

  private async Task<ModerationBulkResult> DismissReportsAsync(
      string[] requestedIds,
      CancellationToken cancellationToken)
  {
    if (!IsStaff || IsQueueInteractionBlocked) return new(0, 0, requestedIds.Length);
    var eligible = requestedIds
        .Select(FindStaffReport)
        .Where(CanResolve)
        .OfType<StaffModerationReport>()
        .DistinctBy(report => report.Id, StringComparer.Ordinal)
        .ToArray();
    var skipped = requestedIds.Length - eligible.Length;
    SetDismissSkippedErrors(requestedIds, eligible);
    BeginBulk(eligible.Select(report => report.Id));
    try
    {
      var results = await Task.WhenAll(eligible.Select(async report =>
      {
        try
        {
          await service.ResolveReportAsync(
              report.Id, ModerationReportResolution.Dismissed, cancellationToken).ConfigureAwait(true);
          return (Report: report, Error: (Exception?)null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          return (Report: report, Error: ex);
        }
      })).ConfigureAwait(true);
      var succeeded = results.Where(result => result.Error is null).Select(result => result.Report.Id).ToArray();
      foreach (var failed in results.Where(result => result.Error is not null))
      {
        rowErrors[failed.Report.Id] = failed.Error!.Message;
      }
      await ApplySuccessfulMutationAsync(succeeded, cancellationToken).ConfigureAwait(true);
      return new(succeeded.Length, results.Length - succeeded.Length, skipped);
    }
    finally
    {
      EndBulk();
    }
  }

  private async Task<ModerationBulkResult> RemoveTargetsAsync(
      string[] requestedIds,
      CancellationToken cancellationToken)
  {
    if (!IsAdministrator || IsQueueInteractionBlocked) return new(0, 0, requestedIds.Length);
    var requested = requestedIds.Select(FindStaffReport).OfType<StaffModerationReport>().ToArray();
    var eligible = requested.Where(CanRemove).GroupBy(report => report.EntityId, StringComparer.Ordinal).ToArray();
    var eligibleReportCount = eligible.Sum(group => group.Count());
    var skipped = requestedIds.Length - eligibleReportCount;
    SetRemoveSkippedErrors(requestedIds, eligible.SelectMany(group => group));
    BeginBulk(eligible.SelectMany(group => group).Select(report => report.Id));
    try
    {
      var results = await Task.WhenAll(eligible.Select(async group =>
      {
        try
        {
          await service.DeleteReportTargetAsync(group.Key, cancellationToken).ConfigureAwait(true);
          return (Reports: group.ToArray(), Error: (Exception?)null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          return (Reports: group.ToArray(), Error: ex);
        }
      })).ConfigureAwait(true);
      var succeeded = results.Where(result => result.Error is null).SelectMany(result => result.Reports).ToArray();
      foreach (var failed in results.Where(result => result.Error is not null))
      {
        foreach (var report in failed.Reports) rowErrors[report.Id] = failed.Error!.Message;
      }
      var allReports = AllStaffReports();
      await ApplySuccessfulMutationAsync(
          succeeded.SelectMany(report =>
              allReports.Where(item => item.EntityId == report.EntityId).Select(item => item.Id)),
          cancellationToken).ConfigureAwait(true);
      return new(succeeded.Length, eligibleReportCount - succeeded.Length, skipped);
    }
    finally
    {
      EndBulk();
    }
  }

  private string[] SelectLoadedReports(IReadOnlyList<StaffModerationReport> reports)
  {
    foreach (var report in reports) selectedIds.Add(report.Id);
    OnPropertyChanged(nameof(SelectedIds));
    return reports.Select(report => report.Id).ToArray();
  }

  private void BeginBulk(IEnumerable<string> reportIds)
  {
    IsBulkActionRunning = true;
    actionIds.UnionWith(reportIds);
    OnPropertyChanged(nameof(IsActionInFlight));
    OnPropertyChanged(nameof(IsQueueMutationRunning));
    OnPropertyChanged(nameof(IsQueueInteractionBlocked));
  }

  private void EndBulk()
  {
    actionIds.Clear();
    IsBulkActionRunning = false;
    OnPropertyChanged(nameof(IsActionInFlight));
    OnPropertyChanged(nameof(IsQueueMutationRunning));
    OnPropertyChanged(nameof(IsQueueInteractionBlocked));
    OnPropertyChanged(nameof(RowErrors));
  }

  private void SetDismissSkippedErrors(
      IEnumerable<string> requestedIds,
      IEnumerable<StaffModerationReport> eligible)
  {
    var eligibleIds = eligible.Select(report => report.Id).ToHashSet(StringComparer.Ordinal);
    foreach (var id in requestedIds.Where(id => !eligibleIds.Contains(id)))
    {
      var report = FindStaffReport(id);
      rowErrors[id] = report switch
      {
        null => "This report is no longer available.",
        { Status: not ModerationReportStatus.Pending } => "Only pending reports can be dismissed.",
        { IsSystemGenerated: true } => "System reports require their dedicated action.",
        _ => "This report cannot be dismissed.",
      };
    }
  }

  private void SetRemoveSkippedErrors(
      IEnumerable<string> requestedIds,
      IEnumerable<StaffModerationReport> eligible)
  {
    var eligibleIds = eligible.Select(report => report.Id).ToHashSet(StringComparer.Ordinal);
    foreach (var id in requestedIds.Where(id => !eligibleIds.Contains(id)))
    {
      var report = FindStaffReport(id);
      rowErrors[id] = report switch
      {
        null => "This report is no longer available.",
        { Status: not ModerationReportStatus.Pending } => "Only pending reports can remove targets.",
        { IsSystemGenerated: true } => "System reports require their dedicated action.",
        _ => "Only post and comment reports can remove targets.",
      };
    }
  }
}
