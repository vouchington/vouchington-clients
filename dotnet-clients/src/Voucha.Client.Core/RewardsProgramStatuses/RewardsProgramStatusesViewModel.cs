using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed partial class RewardsProgramStatusesViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly IRewardsProgramStatusesService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly HashSet<string> mutatingIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, PendingLocalUpsert> localUpserts = new(StringComparer.Ordinal);
  private readonly Dictionary<string, PendingLocalDeletion> pendingDeletions = new(StringComparer.Ordinal);
  private readonly HashSet<int> inFlightReadIds = [];
  private RewardsProgramStatus[] statuses = [];
  private RewardsProgramStatusRow[] rows = [];
  private RewardsProgramStatusOptionRow[] searchRows = [];
  private PageInfo pageInfo = new(null, false, null);
  private RewardsProgramStatusDraft? editDraft;
  private UiText? errorText;
  private UiText? searchErrorText;
  private string searchQuery = string.Empty;
  private bool hasLoaded;
  private bool isLoading;
  private bool isLoadingMore;
  private bool hasContinuationError;
  private bool isCreating;
  private int loadGeneration;
  private int searchGeneration;
  private int nextReadId;

  public RewardsProgramStatusesViewModel(
      IRewardsProgramStatusesService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public IReadOnlyList<RewardsProgramStatus> Statuses => statuses;
  public IReadOnlyList<RewardsProgramStatusRow> Rows => rows;
  public IReadOnlyList<RewardsProgramStatusOptionRow> SearchRows => searchRows;
  public RewardsProgramStatusDraft? EditDraft { get => editDraft; private set => Set(ref editDraft, value); }
  public UiText? ErrorText => errorText;
  public UiText? SearchErrorText => searchErrorText;
  public string? LocalizedErrorMessage => errorText is { } text ? localization.Resolve(text) : null;
  public string? LocalizedSearchErrorMessage => searchErrorText is { } text ? localization.Resolve(text) : null;
  public bool HasError => errorText is not null;
  public bool HasSearchError => searchErrorText is not null;
  public bool HasStatuses => statuses.Length > 0;
  public bool ShowsEmptyState => hasLoaded && !HasStatuses && !HasError;
  public bool HasNextPage => pageInfo.HasNextPage;
  public bool HasEditDraft => EditDraft is not null;
  public bool HasContinuationError { get => hasContinuationError; private set => Set(ref hasContinuationError, value); }
  public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }
  public bool IsLoadingMore { get => isLoadingMore; private set => Set(ref isLoadingMore, value); }
  public bool IsCreating
  {
    get => isCreating;
    private set { if (Set(ref isCreating, value)) OnPropertyChanged(nameof(CanCreate)); }
  }
  public bool CanCreate => !IsCreating;
  public bool IsMutating(string id) => mutatingIds.Contains(id);
  public bool CanSave => EditDraft?.Original is { } original && !IsMutating(original.Id);
  public string SearchQuery
  {
    get => searchQuery;
    set
    {
      if (!Set(ref searchQuery, value ?? string.Empty)) return;
      unchecked { searchGeneration++; }
      SetSearchRows([]);
      SetSearchError(null);
    }
  }

  public void BeginEdit(RewardsProgramStatusRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    EditDraft = new(row.Value);
    OnPropertyChanged(nameof(HasEditDraft));
    OnPropertyChanged(nameof(CanSave));
  }

  public void CancelEdit()
  {
    EditDraft = null;
    OnPropertyChanged(nameof(HasEditDraft));
    OnPropertyChanged(nameof(CanSave));
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    Replace(statuses);
    SetSearchRows(searchRows.Select(row => row.Value));
    OnPropertyChanged(nameof(LocalizedErrorMessage));
    OnPropertyChanged(nameof(LocalizedSearchErrorMessage));
  }

  private void Replace(IEnumerable<RewardsProgramStatus> values)
  {
    statuses = values.GroupBy(value => value.Id, StringComparer.Ordinal).Select(group => group.Last())
        .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    rows = statuses.Select(value => new RewardsProgramStatusRow(value, localization)
    { IsMutating = IsMutating(value.Id) }).ToArray();
    OnPropertyChanged(nameof(Statuses));
    OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(HasStatuses));
    OnPropertyChanged(nameof(ShowsEmptyState));
  }

  private void SetSearchRows(IEnumerable<RewardsProgramStatusOption> values)
  {
    searchRows = values.GroupBy(value => value.Id, StringComparer.Ordinal).Select(group => group.First())
        .Select(value => new RewardsProgramStatusOptionRow(value, localization)).ToArray();
    OnPropertyChanged(nameof(SearchRows));
  }

  private void SetError(UiText? value)
  {
    errorText = value;
    OnPropertyChanged(nameof(ErrorText));
    OnPropertyChanged(nameof(LocalizedErrorMessage));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(ShowsEmptyState));
  }

  private void SetSearchError(UiText? value)
  {
    searchErrorText = value;
    OnPropertyChanged(nameof(SearchErrorText));
    OnPropertyChanged(nameof(LocalizedSearchErrorMessage));
    OnPropertyChanged(nameof(HasSearchError));
  }

  private void Fail(Exception error) => SetError(string.IsNullOrEmpty(error.Message)
      ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));
  private void FailSearch(Exception error) => SetSearchError(string.IsNullOrEmpty(error.Message)
      ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));
  private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(name);
    return true;
  }
  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
