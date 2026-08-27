using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.ImportExport;

public sealed partial class ImportExportViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Current status failures become inline state; stale failures are suppressed.")]
  private async Task MonitorAsync(int operation, string batch, CancellationToken externalToken)
  {
    if (monitorCancellation is not null)
    {
      await monitorCancellation.CancelAsync().ConfigureAwait(true);
    }
    monitorCancellation?.Dispose();
    monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
    var token = monitorCancellation.Token;
    IsMonitoring = true;
    try
    {
      while (!token.IsCancellationRequested && operation == generation && BatchId == batch)
      {
        var status = await service.GetSourceStatusAsync(batch, token).ConfigureAwait(true);
        if (token.IsCancellationRequested || operation != generation || BatchId != batch) return;
        var accepted = status.Import.Id == batch && !IsRegression(status.Import);
        if (accepted)
        {
          Progress = status.Import;
          Results = status.Rows;
        }
        if (accepted && status.Import.IsTerminal()) return;
        await delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(true);
      }
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception ex) { if (operation == generation && BatchId == batch) ErrorMessage = ex.Message; }
    finally
    {
      if (operation == generation && BatchId == batch) IsMonitoring = false;
    }
  }

  private bool IsRegression(Api.RssFeedImportSummary incoming)
  {
    if (Progress is not { } current) return false;
    var currentDone = current.CompletedRows + current.FailedRows;
    var incomingDone = incoming.CompletedRows + incoming.FailedRows;
    return incomingDone < currentDone ||
        (current.CompletedAt is not null && incoming.CompletedAt is null) ||
        incoming.TotalRows != current.TotalRows;
  }
}
