using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationReportsViewModel
{
  public bool IsJudgementPending(string reportId) => judgementAdmissions.Contains(judgementOwner, reportId);

  public bool HasAdmittedJudgementRun(string reportId) =>
      judgementAdmissions.TryGet(judgementOwner, reportId, out var admission) && admission.RunId is not null;

  public async Task<bool> RerunJudgementAsync(string reportId, CancellationToken cancellationToken = default)
  {
    if (!IsStaff || FindStaffReport(reportId) is null || IsQueueInteractionBlocked) return false;
    if (judgementOwner is null)
    {
      rowErrors[reportId] = UiText.Localized(UiMessageKey.NativeSwiftModerationReportsActionUnavailable);
      OnPropertyChanged(nameof(RowErrors));
      return false;
    }
    if (!judgementAdmissions.TryAdmit(judgementOwner, reportId, out var reserved)) return false;
    return await RunActionAsync(reportId, async () =>
    {
      ModerationReportJudgementResponse admission;
      try
      {
        admission = await service.RerunReportJudgementAsync(reportId, cancellationToken).ConfigureAwait(true);
      }
      catch (HttpRequestException error) when (error.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError &&
          error.StatusCode != HttpStatusCode.RequestTimeout)
      {
        judgementAdmissions.Remove(judgementOwner, reportId, reserved.Token);
        throw;
      }
      if (!admission.Queued)
      {
        judgementAdmissions.Remove(judgementOwner, reportId, reserved.Token);
        return;
      }
      if (string.IsNullOrEmpty(admission.RunId)) return;
      judgementAdmissions.SetRunId(judgementOwner, reportId, reserved.Token, admission.RunId);
      await ReadAdmittedJudgementAsync(reportId, admission.RunId, reserved.Token, cancellationToken).ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  public async Task<bool> RefreshPendingJudgementAsync(string reportId, CancellationToken cancellationToken = default)
  {
    if (!IsStaff || FindStaffReport(reportId) is null || IsQueueInteractionBlocked ||
        !judgementAdmissions.TryGet(judgementOwner, reportId, out var admission) ||
        admission.RunId is not { } runId) return false;
    return await RunActionAsync(reportId, () =>
        ReadAdmittedJudgementAsync(reportId, runId, admission.Token, cancellationToken)).ConfigureAwait(true);
  }

  private Task ReadAdmittedJudgementAsync(string reportId, string runId, Guid token, CancellationToken cancellationToken)
  {
    var revision = generation;
    return ReportJudgementRunReconciliation.ReadAsync(reportId, runId, judgementRunReads, judgementRunDelay,
        async (id, admittedRun, cancellation) =>
        {
          var run = await service.GetReportJudgementRunAsync(id, admittedRun, cancellation).ConfigureAwait(true);
          cancellation.ThrowIfCancellationRequested();
          if (revision != generation || FindStaffReport(id) is null ||
              !judgementAdmissions.TryGet(judgementOwner, id, out var current) || current.Token != token ||
              !string.Equals(run.RunId, admittedRun, StringComparison.Ordinal)) throw new OperationCanceledException();
          if (run.IsTerminal) judgementAdmissions.Remove(judgementOwner, id, token);
          return run;
        }, cancellationToken);
  }
}
