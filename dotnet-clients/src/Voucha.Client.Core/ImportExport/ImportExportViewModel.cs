using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ImportExport;

public sealed partial class ImportExportViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IImportExportService service;
  private readonly Func<TimeSpan, CancellationToken, Task> delay;
  private readonly IUiLocalization localization;
  private readonly object exportDocumentLock = new();
  private readonly IDisposable? localeSubscription;
  private CancellationTokenSource? monitorCancellation;
  private CancellationTokenSource? activeOperationCancellation;
  private int generation;
  private string inputText = string.Empty;
  private bool isBusy;
  private bool isMonitoring;
  private string? errorMessage;
  private UiText? localizedErrorText;
  private string? batchId;
  private IReadOnlyList<ImportResult> results = [];
  private RssFeedImportSummary? progress;
  private ExportDocument? exportDocument;
  private SourceExportFeedType selectedSourceExportFeedType;

  public ImportExportViewModel(
      ImportExportRouteContext context,
      IImportExportService service,
      Func<TimeSpan, CancellationToken, Task>? delay = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    ArgumentNullException.ThrowIfNull(context);
    Context = context;
    this.service = service;
    this.delay = delay ?? Task.Delay;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    selectedSourceExportFeedType = SourceExportFeedTypeExtensions.FromApiValue(context.InitialFeedType);
  }

  public ImportExportRouteContext Context { get; }
  public SourceImportFormat SourceImportFormat { get; set; }
  public SourceExportFormat SourceExportFormat { get; set; }
  public IReadOnlyList<UiProtocolOption> SourceExportFeedTypeOptions =>
  [
    Option(SourceExportFeedType.All, UiMessageKey.NativeSwiftImportExportAllSources),
    Option(SourceExportFeedType.Article, UiMessageKey.NativeSwiftImportExportArticle),
    Option(SourceExportFeedType.Podcast, UiMessageKey.NativeSwiftImportExportPodcast),
    Option(SourceExportFeedType.Video, UiMessageKey.NativeSwiftImportExportVideo),
  ];
  public UiProtocolOption SelectedSourceExportFeedTypeOption
  {
    get => SourceExportFeedTypeOptions.Single(option =>
        option.ProtocolValue == SelectedSourceExportFeedType.ToString());
    set
    {
      ArgumentNullException.ThrowIfNull(value);
      if (!Enum.TryParse<SourceExportFeedType>(value.ProtocolValue, out var parsed)) return;
      SelectedSourceExportFeedType = parsed;
      OnPropertyChanged();
    }
  }
  public SourceExportFeedType SelectedSourceExportFeedType
  {
    get => selectedSourceExportFeedType;
    set
    {
      if (SetProperty(ref selectedSourceExportFeedType, value))
        OnPropertyChanged(nameof(SelectedSourceExportFeedTypeOption));
    }
  }
  public string InputText { get => inputText; set => SetProperty(ref inputText, value ?? string.Empty); }
  public bool IsBusy { get => isBusy; private set { if (SetProperty(ref isBusy, value)) { OnPropertyChanged(nameof(CanImport)); OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(CanCancelActiveOperation)); } } }
  public bool IsMonitoring { get => isMonitoring; private set { if (SetProperty(ref isMonitoring, value)) { OnPropertyChanged(nameof(CanResume)); OnPropertyChanged(nameof(CanRetryStatus)); OnPropertyChanged(nameof(CanImport)); OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(CanCancelActiveOperation)); } } }
  public bool CanResume => BatchId is not null && !IsMonitoring && Progress is not null && !Progress.IsTerminal() && ErrorMessage is null;
  public bool CanRetryStatus => BatchId is not null && !IsMonitoring && Progress is not null && !Progress.IsTerminal() && ErrorMessage is not null;
  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set
    {
      localizedErrorText = null;
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanRetryStatus));
      }
    }
  }
  public string? BatchId { get => batchId; private set { if (SetProperty(ref batchId, value)) { OnPropertyChanged(nameof(CanResume)); OnPropertyChanged(nameof(CanRetryStatus)); } } }
  public IReadOnlyList<ImportResult> Results
  {
    get => results;
    private set
    {
      if (!SetProperty(ref results, value)) return;
      OnPropertyChanged(nameof(PresentationResults));
      OnPropertyChanged(nameof(HasPartialFailures));
    }
  }
  public IReadOnlyList<ImportResultPresentation> PresentationResults =>
      Results.Select(result => new ImportResultPresentation(result, localization)).ToArray();
  public RssFeedImportSummary? Progress { get => progress; private set { if (SetProperty(ref progress, value)) { OnPropertyChanged(nameof(CanResume)); OnPropertyChanged(nameof(CanRetryStatus)); OnPropertyChanged(nameof(ProgressValue)); } } }
  public ExportDocument? ExportDocument { get { lock (exportDocumentLock) return exportDocument; } }
  public bool HasExportDocument => ExportDocument is not null;
  public bool CanImport => !IsBusy && !IsMonitoring;
  public bool CanExport => !IsBusy && !IsMonitoring;
  public bool CanCancelActiveOperation => IsBusy || IsMonitoring;
  public bool HasPartialFailures => Results.Any(result => result.IsTerminalFailure()) && Results.Any(result => !result.IsTerminalFailure());
  public bool IsTopics => Context.Owner == ImportExportOwner.Topics;
  public bool IsSources => Context.Owner == ImportExportOwner.Sources;
  public double ProgressValue => Progress is { TotalRows: > 0 } value
      ? (double)(value.CompletedRows + value.FailedRows) / value.TotalRows
      : 0;

  public void StopMonitoring() => monitorCancellation?.Cancel();

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(PresentationResults));
    OnPropertyChanged(nameof(SourceExportFeedTypeOptions));
    OnPropertyChanged(nameof(SelectedSourceExportFeedTypeOption));
  }

  public void Dispose()
  {
    localeSubscription?.Dispose();
    CancelActiveOperations();
    ReplaceExportDocument(null);
  }

  public async Task ShareExportAsync(
      Func<ExportDocument, CancellationToken, Task> shareAsync,
      CancellationToken token = default)
  {
    ArgumentNullException.ThrowIfNull(shareAsync);
    ExportDocumentLease? lease = null;
    try
    {
      lock (exportDocumentLock)
      {
        lease = exportDocument?.AcquireLease();
      }
      if (lease is null) return;
      await shareAsync(lease.Document, token).ConfigureAwait(true);
    }
    finally
    {
      lease?.Dispose();
    }
  }

  private void SetLocalizedError(UiText text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(CanResume));
    OnPropertyChanged(nameof(CanRetryStatus));
  }

  private UiProtocolOption Option(SourceExportFeedType value, UiMessageKey key) =>
      new(value.ToString(), UiText.Localized(key), localization);

  private void ReplaceExportDocument(ExportDocument? document)
  {
    ExportDocument? previous;
    lock (exportDocumentLock)
    {
      previous = exportDocument;
      exportDocument = document;
    }
    previous?.Dispose();
    OnPropertyChanged(nameof(ExportDocument));
    OnPropertyChanged(nameof(HasExportDocument));
  }
}
