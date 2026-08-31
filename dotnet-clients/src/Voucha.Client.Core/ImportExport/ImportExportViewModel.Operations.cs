using Voucha.Client.Core.Api;
using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.ImportExport;

public sealed partial class ImportExportViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Current native operation failures become inline state; stale failures are suppressed.")]
  public async Task ImportAsync(CancellationToken token = default)
  {
    if (IsBusy || IsMonitoring) return;
    ErrorMessage = null;
    ExportDocument?.Dispose();
    ExportDocument = null;
    var (operation, operationToken) = BeginActiveOperation(token);
    if (Context.Owner == ImportExportOwner.Topics)
    {
      await ImportTopicsAsync(operation, operationToken).ConfigureAwait(true);
      return;
    }

    var request = CreateSourceRequest();
    if (ImportExportValidation.Validate(request) is { } error)
    {
      SetLocalizedError(error);
      EndActiveOperation(operation);
      return;
    }

    ResetSourceImportState();
    IsBusy = true;
    try
    {
      var submission = await service.SubmitSourcesAsync(request, operationToken).ConfigureAwait(true);
      if (operationToken.IsCancellationRequested || operation != generation) return;
      BatchId = submission.Import.Id;
      Progress = submission.Import;
      Results = [];
      await MonitorAsync(operation, submission.Import.Id, operationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) when (operationToken.IsCancellationRequested) { }
    catch (Exception ex) { if (operation == generation) ErrorMessage = ex.Message; }
    finally { EndActiveOperation(operation); }
  }

  public async Task ResumeMonitoringAsync(CancellationToken token = default)
  {
    if ((!CanResume && !CanRetryStatus) || BatchId is not { } batch || IsBusy) return;
    ErrorMessage = null;
    var (operation, operationToken) = BeginActiveOperation(token);
    try { await MonitorAsync(operation, batch, operationToken).ConfigureAwait(true); }
    finally { EndActiveOperation(operation); }
  }

  public Task TryStatusAgainAsync(CancellationToken token = default) => ResumeMonitoringAsync(token);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The native view model surfaces transport and serialization failures as inline state.")]
  public async Task ExportAsync(CancellationToken token = default)
  {
    if (IsBusy || IsMonitoring) return;
    var (operation, operationToken) = BeginActiveOperation(token);
    IsBusy = true;
    ErrorMessage = null;
    ExportDocument?.Dispose();
    ExportDocument = null;
    try
    {
      var document = Context.Owner == ImportExportOwner.Topics
          ? await service.ExportTopicsAsync(operationToken).ConfigureAwait(true)
          : await service.ExportSourcesAsync(
              SelectedSourceExportFeedType.ApiValue(),
              SourceExportFormat,
              operationToken).ConfigureAwait(true);
      if (!operationToken.IsCancellationRequested && operation == generation) ExportDocument = document;
      else document.Dispose();
    }
    catch (OperationCanceledException) when (operationToken.IsCancellationRequested) { }
    catch (Exception ex) { if (operation == generation) ErrorMessage = ex.Message; }
    finally { EndActiveOperation(operation); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Current topic failures become inline state; stale failures are suppressed.")]
  private async Task ImportTopicsAsync(int operation, CancellationToken token)
  {
    var names = ImportExportValidation.Lines(InputText);
    if (ImportExportValidation.ValidateTopics(names) is { } error)
    {
      SetLocalizedError(error);
      EndActiveOperation(operation);
      return;
    }
    IsBusy = true;
    try
    {
      var response = await service.ImportTopicsAsync(names, token).ConfigureAwait(true);
      if (!token.IsCancellationRequested && operation == generation) Results = response.Results;
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception ex) { if (operation == generation) ErrorMessage = ex.Message; }
    finally { EndActiveOperation(operation); }
  }

  private SourceImportRequest CreateSourceRequest() => SourceImportFormat switch
  {
    SourceImportFormat.Urls => new(SourceImportFormat.Urls, ImportExportValidation.Lines(InputText)),
    SourceImportFormat.Csv => new(SourceImportFormat.Csv, Text: InputText),
    SourceImportFormat.Opml => new(SourceImportFormat.Opml, Text: InputText),
    _ => throw new ArgumentOutOfRangeException(),
  };
}
