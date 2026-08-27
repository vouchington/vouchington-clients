using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly ISpendingCategoriesService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private SpendingCategory[] values = [];
  private SpendingCategoryRow[] rows = [];
  private SpendingCategoryOption[] searchResults = [];
  private SpendingCategoryOptionRow[] searchRows = [];
  private PageInfo pageInfo = new(null, false, null);
  private SpendingCategoryDraft? editDraft;
  private string searchQuery = string.Empty;
  private UiText? errorText;
  private UiText? searchErrorText;
  private bool hasContinuationError;
  private bool loading;
  private bool loadingMore;
  private bool creating;
  private bool loaded;
  private int loadGeneration;
  private int searchGeneration;
  private int nextReadId;
  private readonly HashSet<string> mutating = new(StringComparer.Ordinal);
  private readonly HashSet<int> inFlightReadIds = [];
  private readonly Dictionary<string, PendingLocalUpsert> localUpserts = new(StringComparer.Ordinal);
  private readonly Dictionary<string, PendingLocalDeletion> pendingDeletions = new(StringComparer.Ordinal);

  public SpendingCategoriesViewModel(
      ISpendingCategoriesService service,
      IUiLocalization? localization = null,
      IUiLocaleController? controller = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    CreateDraft = new(this.localization.Culture);
    localeSubscription = controller?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public SpendingCategoryDraft CreateDraft { get; private set; }
  public SpendingCategoryDraft? EditDraft { get => editDraft; private set => Set(ref editDraft, value); }
  public IReadOnlyList<SpendingCategory> Categories => values;
  public IReadOnlyList<SpendingCategoryRow> Rows => rows;
  public IReadOnlyList<SpendingCategoryOptionRow> SearchRows => searchRows;
  public string SearchQuery
  {
    get => searchQuery;
    set
    {
      if (!Set(ref searchQuery, value ?? string.Empty)) return;
      unchecked { searchGeneration++; }
      SetSearchResults([]);
      SetSearchError(null);
    }
  }
  public UiText? ErrorText => errorText;
  public string? Error => errorText is { } value ? localization.Resolve(value) : null;
  public bool HasError => errorText is not null;
  public string? LocalizedSearchErrorMessage => searchErrorText is { } value ? localization.Resolve(value) : null;
  public bool HasSearchError => searchErrorText is not null;
  public bool HasContinuationError { get => hasContinuationError; private set => Set(ref hasContinuationError, value); }
  public bool IsLoading { get => loading; private set => Set(ref loading, value); }
  public bool IsLoadingMore { get => loadingMore; private set => Set(ref loadingMore, value); }
  public bool IsCreating { get => creating; private set => Set(ref creating, value); }
  public bool HasNextPage => pageInfo.HasNextPage;
  public bool HasEditDraft => EditDraft is not null;
  public bool IsMutating(string id) => mutating.Contains(id);
  public string MonthlyFrequencyText => localization.Localize(UiMessageKey.ExtractedSpendingCategoriesManagerFrequencySelectMonthly9b11f6b7);
  public string AnnuallyFrequencyText => localization.Localize(UiMessageKey.ExtractedSpendingCategoriesManagerFrequencySelectAnnually1ec9d1d5);
  public string DeleteTitle => localization.Localize(UiMessageKey.ExtractedSpendingCategoriesManagerCategorySummaryRemove9fe2f243);
  public string DeleteConfirmText => localization.Localize(UiMessageKey.ExtractedSpendingCategoriesManagerCategorySummaryConfirmEebdd24a);
  public string DeleteCancelText => localization.Localize(UiMessageKey.ExtractedSpendingCategoriesManagerCategorySummaryCancel19766ed6);

  public Task EnsureLoadedAsync(CancellationToken token = default) => loaded ? Task.CompletedTask : LoadAsync(token);
  public Task RetryAsync(CancellationToken token = default)
  {
    if (!HasContinuationError) return LoadAsync(token);
    HasContinuationError = false;
    SetError(null);
    return LoadMoreAsync(token);
  }


  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Search failures are presentation state.")]
  public async Task SearchAsync(CancellationToken token = default)
  {
    var query = SearchQuery.Trim();
    var generation = unchecked(++searchGeneration);
    if (query.Length == 0) { SetSearchResults([]); return; }
    SetSearchError(null);
    try
    {
      var results = await service.SearchAsync(query, token).ConfigureAwait(true);
      if (generation == searchGeneration && SearchQuery.Trim() == query)
        SetSearchResults(results);
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception error) { if (generation == searchGeneration) FailSearch(error); }
  }


  public void Dispose() => localeSubscription?.Dispose();

  private void Replace(IEnumerable<SpendingCategory> next)
  {
    values = next.GroupBy(value => value.Id, StringComparer.Ordinal).Select(group => group.Last()).ToArray();
    RebuildRows();
  }

  private void RebuildRows()
  {
    rows = values.Select(value => new SpendingCategoryRow(value, localization)).ToArray();
    Notify(nameof(Categories)); Notify(nameof(Rows)); Notify(nameof(HasNextPage));
  }
  private void RebuildSearchRows()
  {
    searchRows = searchResults.Select(value => new SpendingCategoryOptionRow(value, localization)).ToArray();
    Notify(nameof(SearchRows));
  }
  private void SetSearchResults(IEnumerable<SpendingCategoryOption> next)
  {
    searchResults = next.ToArray();
    RebuildSearchRows();
  }
  private void SetError(UiText? next) { errorText = next; Notify(nameof(ErrorText)); Notify(nameof(Error)); Notify(nameof(HasError)); }
  private void SetSearchError(UiText? next) { searchErrorText = next; Notify(nameof(LocalizedSearchErrorMessage)); Notify(nameof(HasSearchError)); }
  private void Fail(Exception error) => SetError(string.IsNullOrEmpty(error.Message) ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));
  private void FailSearch(Exception error) => SetSearchError(string.IsNullOrEmpty(error.Message) ? UiText.Localized(UiMessageKey.NativeCommonRetry) : UiText.Verbatim(error.Message));
  private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Notify(name!); return true; }
}
