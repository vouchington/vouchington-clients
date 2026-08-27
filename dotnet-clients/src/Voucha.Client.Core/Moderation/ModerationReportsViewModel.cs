using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public enum ModerationReportsMode { Grouped, Flat }

public sealed partial class ModerationReportsViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IModerationService service;
  private readonly HashSet<string> selectedIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> actionIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> successfulReportIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, string> rowErrors = new(StringComparer.Ordinal);
  private readonly IDisposable? localeSubscription;
  private CancellationTokenSource? loadCancellation;
  private IReadOnlyList<StaffModerationReport> staffReports = [];
  private IReadOnlyList<MemberModerationReport> memberReports = [];
  private IReadOnlyList<StaffModerationReportEntityCluster> clusters = [];
  private IReadOnlyList<StaffModerationReportDuplicateCluster> duplicateClusters = [];
  private ModerationReportStatus status = ModerationReportStatus.Pending;
  private ModerationReportSort sort;
  private ModerationReportsMode mode;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? notice;
  private string? cursor;
  private bool hasNextPage;
  private bool isBulkActionRunning;
  private int generation;
  private int groupedPageCount;

  public ModerationReportsViewModel(
      IModerationService service,
      NavigationViewer viewer,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    ArgumentNullException.ThrowIfNull(viewer);
    IsAdministrator = viewer.Roles.Contains("administrator", StringComparer.Ordinal);
    IsStaff = IsAdministrator || viewer.Roles.Contains("moderator", StringComparer.Ordinal);
    mode = IsStaff ? ModerationReportsMode.Grouped : ModerationReportsMode.Flat;
    sort = IsStaff ? ModerationReportSort.Severity : ModerationReportSort.CreatedAtDesc;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsStaff { get; }
  public bool IsAdministrator { get; }
  public bool IsMemberReadOnly => !IsStaff;
  public ModerationReportStatus Status => status;
  public ModerationReportSort Sort => sort;
  public ModerationReportsMode Mode => mode;
  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value)) OnPropertyChanged(nameof(IsQueueInteractionBlocked));
    }
  }
  public bool IsLoading => State == LoadState.Loading;
  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public string? Notice { get => notice; private set => SetProperty(ref notice, value); }
  public bool HasNextPage { get => hasNextPage; private set => SetProperty(ref hasNextPage, value); }
  public bool IsBulkActionRunning
  {
    get => isBulkActionRunning;
    private set
    {
      if (!SetProperty(ref isBulkActionRunning, value)) return;
      OnPropertyChanged(nameof(IsQueueMutationRunning));
      OnPropertyChanged(nameof(IsQueueInteractionBlocked));
    }
  }
  public IReadOnlyList<StaffModerationReport> StaffReports { get => staffReports; private set => SetProperty(ref staffReports, value); }
  public IReadOnlyList<MemberModerationReport> MemberReports { get => memberReports; private set => SetProperty(ref memberReports, value); }
  public IReadOnlyList<StaffModerationReportEntityCluster> Clusters { get => clusters; private set => SetProperty(ref clusters, value); }
  public IReadOnlyList<StaffModerationReportDuplicateCluster> DuplicateClusters { get => duplicateClusters; private set => SetProperty(ref duplicateClusters, value); }
  public IReadOnlySet<string> SelectedIds => selectedIds;
  public IReadOnlyDictionary<string, string> RowErrors => rowErrors;
  public bool IsQueueMutationRunning => IsBulkActionRunning || actionIds.Count > 0;
  public bool IsQueueInteractionBlocked => IsLoading || IsQueueMutationRunning;

  public bool IsSelected(string reportId) => selectedIds.Contains(reportId);
  public bool IsActionInFlight(string reportId) =>
      actionIds.Contains(reportId) || IsQueueInteractionBlocked;

  public void ToggleSelection(string reportId)
  {
    if (!IsStaff || IsQueueInteractionBlocked) return;
    if (!selectedIds.Add(reportId)) selectedIds.Remove(reportId);
    OnPropertyChanged(nameof(SelectedIds));
  }

  public async Task SetFiltersAsync(
      ModerationReportStatus nextStatus,
      ModerationReportsMode nextMode,
      ModerationReportSort nextSort,
      CancellationToken cancellationToken = default)
  {
    if (IsQueueMutationRunning) return;
    var allowedMode = IsStaff ? nextMode : ModerationReportsMode.Flat;
    var allowedSort = IsStaff ? nextSort : ModerationReportSort.CreatedAtDesc;
    if (status == nextStatus && mode == allowedMode && sort == allowedSort) return;
    status = nextStatus;
    mode = allowedMode;
    sort = allowedSort;
    OnPropertyChanged(nameof(Status));
    OnPropertyChanged(nameof(Mode));
    OnPropertyChanged(nameof(Sort));
    ResetQueue();
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  private void ResetQueue()
  {
    CancelLoad();
    cursor = null;
    HasNextPage = false;
    StaffReports = [];
    MemberReports = [];
    Clusters = [];
    DuplicateClusters = [];
    groupedPageCount = 0;
    successfulReportIds.Clear();
    selectedIds.Clear();
    rowErrors.Clear();
    ErrorMessage = null;
    Notice = null;
    State = LoadState.Idle;
    OnPropertyChanged(nameof(SelectedIds));
    OnPropertyChanged(nameof(RowErrors));
  }

  private void CancelLoad()
  {
    generation++;
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = null;
  }

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(StaffReports));

  public void Dispose()
  {
    localeSubscription?.Dispose();
    CancelLoad();
  }
}
