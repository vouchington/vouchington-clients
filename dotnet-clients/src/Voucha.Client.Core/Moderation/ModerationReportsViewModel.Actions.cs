using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationReportsViewModel
{
  public Task<bool> ReviewAsync(string reportId, CancellationToken cancellationToken = default) =>
      ResolveAsync(reportId, ModerationReportResolution.Reviewed, cancellationToken);

  public Task<bool> DismissAsync(string reportId, CancellationToken cancellationToken = default) =>
      ResolveAsync(reportId, ModerationReportResolution.Dismissed, cancellationToken);

  public async Task<bool> ResolveAsync(
      string reportId,
      ModerationReportResolution resolution,
      CancellationToken cancellationToken = default)
  {
    var report = FindStaffReport(reportId);
    if (!CanResolve(report)) return false;
    return await RunActionAsync(
        reportId,
        async () =>
        {
          await service.ResolveReportAsync(reportId, resolution, cancellationToken).ConfigureAwait(true);
          await ApplySuccessfulMutationAsync([reportId], cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
  }

  public async Task<bool> RerunJudgementAsync(string reportId, CancellationToken cancellationToken = default)
  {
    if (!IsStaff || FindStaffReport(reportId) is null) return false;
    return await RunActionAsync(
        reportId,
        async () => await service.RerunReportJudgementAsync(reportId, cancellationToken).ConfigureAwait(true))
        .ConfigureAwait(true);
  }

  public async Task<bool> IssueWarningAsync(
      string reportId,
      string reason,
      string? publicMessage,
      CancellationToken cancellationToken = default)
  {
    var report = FindStaffReport(reportId);
    if (!CanIssueWarning(report) || report?.TargetUserId is not { } targetUserId) return false;
    var request = new IssueAdminWarningRequest(targetUserId, reason, publicMessage, report.Id);
    return await RunActionAsync(
        reportId,
        async () =>
        {
          await service.IssueWarningAsync(request, cancellationToken).ConfigureAwait(true);
          await ApplySuccessfulMutationAsync([reportId], cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
  }

  public Task<bool> ConfirmBanEvasionAsync(string reportId, CancellationToken cancellationToken = default) =>
      RunBanEvasionAsync(reportId, confirm: true, cancellationToken);

  public Task<bool> DismissBanEvasionAsync(string reportId, CancellationToken cancellationToken = default) =>
      RunBanEvasionAsync(reportId, confirm: false, cancellationToken);

  public async Task<bool> RemoveTargetAsync(string reportId, CancellationToken cancellationToken = default)
  {
    var report = FindStaffReport(reportId);
    if (!CanRemove(report)) return false;
    return await RunActionAsync(
        reportId,
        async () =>
        {
          await service.DeleteReportTargetAsync(report!.EntityId, cancellationToken).ConfigureAwait(true);
          await ApplySuccessfulMutationAsync(
              AllStaffReports().Where(item => item.EntityId == report.EntityId).Select(item => item.Id),
              cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
  }

  public bool CanResolve(StaffModerationReport? report) =>
      IsStaff && report is { Status: ModerationReportStatus.Pending, IsSystemGenerated: false } &&
      !successfulReportIds.Contains(report.Id);

  public bool CanRemove(StaffModerationReport? report) =>
      IsAdministrator && report is { Status: ModerationReportStatus.Pending, IsSystemGenerated: false } &&
      (report.EntityType is "post" or "comment") && !successfulReportIds.Contains(report.Id);

  public bool CanIssueWarning(StaffModerationReport? report) =>
      IsStaff && report is
      {
        Status: ModerationReportStatus.Pending,
        IsSystemGenerated: false,
        TargetUserId: not null,
      } && !successfulReportIds.Contains(report.Id);

  public bool CanHandleBanEvasion(StaffModerationReport? report) =>
      IsStaff && report is
      {
        Status: ModerationReportStatus.Pending,
        IsSystemGenerated: true,
        CommunityBanEvasion: not null,
      } && !successfulReportIds.Contains(report.Id);

  private async Task<bool> RunBanEvasionAsync(
      string reportId,
      bool confirm,
      CancellationToken cancellationToken)
  {
    var report = FindStaffReport(reportId);
    var context = report?.CommunityBanEvasion;
    if (!CanHandleBanEvasion(report) || report is null || context is null) return false;
    return await RunActionAsync(
        reportId,
        async () =>
        {
          if (confirm)
          {
            await service.ConfirmBanEvasionAsync(context.CommunityId, report.EntityId, cancellationToken).ConfigureAwait(true);
          }
          else
          {
            await service.DismissBanEvasionAsync(context.CommunityId, report.EntityId, cancellationToken).ConfigureAwait(true);
          }
          await ApplySuccessfulMutationAsync([reportId], cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
  }

  private async Task<bool> RunActionAsync(string reportId, Func<Task> action)
  {
    if (IsQueueInteractionBlocked) return false;
    actionIds.Add(reportId);
    rowErrors.Remove(reportId);
    OnPropertyChanged(nameof(RowErrors));
    OnPropertyChanged(nameof(IsActionInFlight));
    OnPropertyChanged(nameof(IsQueueMutationRunning));
    OnPropertyChanged(nameof(IsQueueInteractionBlocked));
    try
    {
      await action().ConfigureAwait(true);
      return true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      rowErrors[reportId] = ex.Message;
      OnPropertyChanged(nameof(RowErrors));
      return false;
    }
    finally
    {
      actionIds.Remove(reportId);
      OnPropertyChanged(nameof(IsActionInFlight));
      OnPropertyChanged(nameof(IsQueueMutationRunning));
      OnPropertyChanged(nameof(IsQueueInteractionBlocked));
    }
  }
}
