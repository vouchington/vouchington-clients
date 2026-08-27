using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PointValuations;

public sealed partial class PointValuationsViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly IPointValuationsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private PointValuation[] valuations = [];
  private PointValuationRow[] rows = [];
  private RewardsProgramOption[] searchResults = [];
  private RewardsProgramRow[] searchRows = [];
  private PageInfo pageInfo = new(null, false, null);
  private PointValuationDraft? editDraft;
  private string searchQuery = string.Empty;
  private UiText? errorText;
  private UiText? searchErrorText;
  private bool hasContinuationError;
  private bool isLoading;
  private bool isLoadingMore;
  private bool isCreating;
  private bool hasLoaded;
  private int loadGeneration;
  private int searchGeneration;
  private readonly HashSet<string> mutatingIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, PendingLocalDeletion> pendingDeletions = new(StringComparer.Ordinal);
  private readonly Dictionary<string, PendingLocalUpsert> localUpserts = new(StringComparer.Ordinal);
  private readonly HashSet<int> inFlightReadIds = [];
  private int nextReadId;

  public PointValuationsViewModel(
      IPointValuationsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    CreateDraft = new(this.localization.Culture);
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public PointValuationDraft CreateDraft { get; private set; }
  public PointValuationDraft? EditDraft { get => editDraft; private set => Set(ref editDraft, value); }
  public IReadOnlyList<PointValuation> Valuations => valuations;
  public IReadOnlyList<PointValuationRow> Rows => rows;
  public IReadOnlyList<RewardsProgramRow> SearchRows => searchRows;
  public string SearchQuery
  {
    get => searchQuery;
    set
    {
      value ??= string.Empty;
      if (!Set(ref searchQuery, value)) return;
      unchecked { searchGeneration++; }
      SetSearchResults([]);
      SetSearchError(null);
    }
  }
  public UiText? ErrorText => errorText;
  public string? LocalizedErrorMessage => errorText is { } value ? localization.Resolve(value) : null;
  public bool HasError => errorText is not null;
  public string? LocalizedSearchErrorMessage => searchErrorText is { } value ? localization.Resolve(value) : null;
  public bool HasSearchError => searchErrorText is not null;
  public bool HasValuations => valuations.Length > 0;
  public bool ShowsEmptyState => hasLoaded && !HasValuations && !HasError;
  public bool HasNextPage => pageInfo.HasNextPage;
  public bool HasContinuationError { get => hasContinuationError; private set => Set(ref hasContinuationError, value); }
  public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }
  public bool IsLoadingMore { get => isLoadingMore; private set => Set(ref isLoadingMore, value); }
  public bool IsCreating { get => isCreating; private set => Set(ref isCreating, value); }
  public bool HasEditDraft => EditDraft is not null;
  public bool IsMutating(string id) => mutatingIds.Contains(id);

  public void BeginEdit(PointValuationRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    EditDraft = new(row.Value, localization.Culture);
    OnPropertyChanged(nameof(HasEditDraft));
  }

  public void CancelEdit() { EditDraft = null; OnPropertyChanged(nameof(HasEditDraft)); }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    rows = valuations.Select(value => new PointValuationRow(value, localization)).ToArray();
    searchRows = searchResults.Select(value => new RewardsProgramRow(value, localization)).ToArray();
    OnPropertyChanged(nameof(Rows)); OnPropertyChanged(nameof(SearchRows));
    OnPropertyChanged(nameof(LocalizedErrorMessage));
    OnPropertyChanged(nameof(LocalizedSearchErrorMessage));
    CreateDraft.ApplyCulture(localization.Culture);
    EditDraft?.ApplyCulture(localization.Culture);
  }

  private void Replace(IEnumerable<PointValuation> values)
  {
    valuations = values.GroupBy(value => value.Id, StringComparer.Ordinal).Select(group => group.Last())
        .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    rows = valuations.Select(value => new PointValuationRow(value, localization)).ToArray();
    OnPropertyChanged(nameof(Valuations)); OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(HasValuations)); OnPropertyChanged(nameof(ShowsEmptyState));
  }

  private void SetSearchResults(IEnumerable<RewardsProgramOption> values)
  {
    searchResults = values.ToArray();
    searchRows = searchResults.Select(value => new RewardsProgramRow(value, localization)).ToArray();
    OnPropertyChanged(nameof(SearchRows));
  }

  private void SetError(UiText? value)
  {
    errorText = value;
    OnPropertyChanged(nameof(ErrorText)); OnPropertyChanged(nameof(LocalizedErrorMessage));
    OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(ShowsEmptyState));
  }

  private void SetSearchError(UiText? value)
  {
    searchErrorText = value;
    OnPropertyChanged(nameof(LocalizedSearchErrorMessage)); OnPropertyChanged(nameof(HasSearchError));
  }

  private void Fail(Exception error) => SetError(string.IsNullOrEmpty(error.Message)
      ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));

  private void FailSearch(Exception error) => SetSearchError(string.IsNullOrEmpty(error.Message)
      ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));

  private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value; OnPropertyChanged(name); return true;
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
