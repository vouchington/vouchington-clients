namespace Voucha.Client.Core.ImportExport;

public sealed partial class ImportExportViewModel
{
  public void CancelActiveOperations()
  {
    generation++;
    activeOperationCancellation?.Cancel();
    activeOperationCancellation?.Dispose();
    activeOperationCancellation = null;
    monitorCancellation?.Cancel();
    monitorCancellation?.Dispose();
    monitorCancellation = null;
    IsBusy = false;
    IsMonitoring = false;
  }

  public void ReportExternalFailure(Exception exception, CancellationToken operationToken)
  {
    ArgumentNullException.ThrowIfNull(exception);
    if (!operationToken.IsCancellationRequested) ErrorMessage = exception.Message;
  }

  private (int Id, CancellationToken Token) BeginActiveOperation(CancellationToken externalToken)
  {
    var id = ++generation;
    activeOperationCancellation?.Cancel();
    activeOperationCancellation?.Dispose();
    activeOperationCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
    return (id, activeOperationCancellation.Token);
  }

  private void EndActiveOperation(int operation)
  {
    if (operation != generation) return;
    activeOperationCancellation?.Dispose();
    activeOperationCancellation = null;
    IsBusy = false;
  }

  private void ResetSourceImportState()
  {
    monitorCancellation?.Cancel();
    monitorCancellation?.Dispose();
    monitorCancellation = null;
    IsMonitoring = false;
    BatchId = null;
    Progress = null;
    Results = [];
  }
}
