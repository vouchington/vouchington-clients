using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

internal static class ReportJudgementRunReconciliation
{
  internal const int DefaultReadCount = 20;
  internal static readonly TimeSpan ReadSpacing = TimeSpan.FromSeconds(3);

  internal static async Task ReadAsync(
      string reportId,
      string runId,
      int maximumReads,
      Func<CancellationToken, Task> delay,
      Func<string, string, CancellationToken, Task<ModerationReportJudgementRunResponse>> read,
      CancellationToken cancellationToken)
  {
    if (maximumReads < 1 || string.IsNullOrEmpty(reportId) || string.IsNullOrEmpty(runId))
    {
      return;
    }

    for (var attempt = 0; attempt < maximumReads; attempt++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var run = await read(reportId, runId, cancellationToken).ConfigureAwait(false);
      if (run.Completed)
      {
        return;
      }

      if (attempt + 1 < maximumReads)
      {
        await delay(cancellationToken).ConfigureAwait(false);
      }
    }
  }
}
