using Voucha.Client.App.Support;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ImportExportPage : ContentPage, IDisposable
{
  private readonly ImportExportViewModel viewModel;
  private readonly IImportExportFileAdapter files;
  private CancellationTokenSource lifecycleCancellation = new();
  private bool hadNavigationParent;
  private bool disposed;

  public ImportExportPage(
      ImportExportRouteContext context,
      IImportExportService service,
      IImportExportFileAdapter files,
      IUiLocalization localization,
      IUiLocaleController localeController)
  {
    InitializeComponent();
    this.files = files;
    viewModel = new(context, service, localization: localization, localeController: localeController);
    BindingContext = viewModel;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    if (disposed) return;
    if (!lifecycleCancellation.IsCancellationRequested) return;
    lifecycleCancellation.Dispose();
    lifecycleCancellation = new();
  }

  protected override void OnDisappearing()
  {
    if (!disposed)
    {
      lifecycleCancellation.Cancel();
      viewModel.CancelActiveOperations();
    }
    base.OnDisappearing();
  }

  protected override void OnParentSet()
  {
    base.OnParentSet();
    if (Parent is not null) hadNavigationParent = true;
    else if (hadNavigationParent) Dispose();
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    lifecycleCancellation.Cancel();
    lifecycleCancellation.Dispose();
    viewModel.Dispose();
    BindingContext = null;
    GC.SuppressFinalize(this);
  }

  private void OnUrlsClicked(object? sender, EventArgs e) => viewModel.SourceImportFormat = SourceImportFormat.Urls;
  private void OnCsvClicked(object? sender, EventArgs e) => viewModel.SourceImportFormat = SourceImportFormat.Csv;
  private void OnOpmlClicked(object? sender, EventArgs e) => viewModel.SourceImportFormat = SourceImportFormat.Opml;
  private async void OnImportClicked(object? sender, EventArgs e) => await viewModel.ImportAsync();
  private void OnStopClicked(object? sender, EventArgs e) => viewModel.StopMonitoring();
  private void OnCancelClicked(object? sender, EventArgs e) => viewModel.CancelActiveOperations();
  private async void OnResumeClicked(object? sender, EventArgs e) => await viewModel.ResumeMonitoringAsync();
  private async void OnRetryStatusClicked(object? sender, EventArgs e) => await viewModel.TryStatusAgainAsync();
  private async void OnExportTopicsClicked(object? sender, EventArgs e) => await viewModel.ExportAsync();

  private async void OnExportCsvClicked(object? sender, EventArgs e)
  {
    viewModel.SourceExportFormat = SourceExportFormat.Csv;
    await viewModel.ExportAsync();
  }

  private async void OnExportOpmlClicked(object? sender, EventArgs e)
  {
    viewModel.SourceExportFormat = SourceExportFormat.Opml;
    await viewModel.ExportAsync();
  }

  private async void OnChooseFileClicked(object? sender, EventArgs e)
  {
    var token = lifecycleCancellation.Token;
    try
    {
      var file = await files.PickSourceFileAsync(token);
      if (file is null || token.IsCancellationRequested) return;
      viewModel.SourceImportFormat = file.Format;
      viewModel.InputText = file.Text;
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception ex) { viewModel.ReportExternalFailure(ex, token); }
  }

  private async void OnShareClicked(object? sender, EventArgs e)
  {
    if (viewModel.ExportDocument is { } document)
    {
      var token = lifecycleCancellation.Token;
      try { await files.ShareAsync(document, token); }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
      catch (Exception ex) { viewModel.ReportExternalFailure(ex, token); }
    }
  }
}
