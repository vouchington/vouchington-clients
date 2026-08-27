using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed record AiCostRow(CommunityAiCostTotal Total, IUiLocalization Localization)
{
  public string InvariantCommunitySlug => Total.CommunitySlug;
  public string LocalizedRequests => Localization.FormatNumber(Total.RequestCount);
  public string LocalizedInputTokens => Localization.FormatNumber(Total.TotalInputTokens);
  public string LocalizedOutputTokens => Localization.FormatNumber(Total.TotalOutputTokens);
  public string LocalizedCost => Localization.FormatCurrency(Total.TotalCost.ScaledAmount, Total.TotalCost.Currency, Total.TotalCost.Scale);
  public bool HasUnpricedRequests => Total.UnpricedRequestCount > 0;
  public string LocalizedUnpriced => Localization.Format(
      UiMessageKey.ExtractedAiCostsPageUnpricedRequestCount,
      ("count", Total.UnpricedRequestCount));
}

public sealed class AiCostsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IEngineeringService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly CursorPaginationState<CommunityAiCostTotal, string> pages = new(total => total.CommunityId);
  private IReadOnlyList<AiCostRow> rows = [];
  private bool isRefreshing;
  private bool isLoadingMore;
  private string? errorMessage;
  private int refreshGeneration;

  public AiCostsViewModel(IEngineeringService service, IUiLocalization? localization = null, IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<AiCostRow> Rows { get => rows; private set => SetProperty(ref rows, value); }
  public bool IsRefreshing { get => isRefreshing; private set { if (SetProperty(ref isRefreshing, value)) OnPropertyChanged(nameof(HasMore)); } }
  public bool IsLoadingMore { get => isLoadingMore; private set => SetProperty(ref isLoadingMore, value); }
  public bool HasMore => pages.HasLoadedPage && pages.EndCursor is not null && !IsRefreshing && !HasError && pages.HasMore;
  public bool HasRows => Rows.Count > 0;
  public bool ShowsEmptyState => pages.HasLoadedPage && !HasRows && !HasError;
  public bool HasContinuationError => pages.LastError is not null && HasRows;
  public bool HasError => !string.IsNullOrWhiteSpace(errorMessage);
  public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) { OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(HasMore)); } } }

  public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
      pages.HasLoadedPage ? Task.CompletedTask : RefreshAsync(cancellationToken);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Refresh failures preserve visible AI cost rows and become retryable presentation state.")]
  public async Task RefreshAsync(CancellationToken cancellationToken = default)
  {
    if (IsRefreshing) return;
    IsRefreshing = true;
    ErrorMessage = null;
    pages.InvalidateRequestsPreservingPage();
    IsLoadingMore = false;
    var generation = unchecked(++refreshGeneration);
    try
    {
      var response = await service.FetchAiCostsAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
      if (generation != refreshGeneration) return;
      pages.Reset(response.Results);
      pages.RestoreContinuation(response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
      RebuildRows();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { ErrorMessage = error.Message; }
    finally { IsRefreshing = false; OnStateChanged(); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve visible AI cost rows and become retryable presentation state.")]
  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (IsRefreshing) return;
    if (!pages.HasLoadedPage || pages.EndCursor is null) return;
    var request = pages.BeginNextPage();
    if (request is null) return;
    IsLoadingMore = true;
    try
    {
      var response = await service.FetchAiCostsAsync(request.Cursor, cancellationToken).ConfigureAwait(true);
      if (pages.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage)) RebuildRows();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { pages.Cancel(request); }
    catch (Exception error) { pages.Fail(request, error.Message); }
    finally { IsLoadingMore = pages.IsLoading; OnStateChanged(); }
  }

  public Task RetryAsync(CancellationToken cancellationToken = default) =>
      HasContinuationError ? LoadMoreAsync(cancellationToken) : RefreshAsync(cancellationToken);

  public void OnUiLocaleChanged() => RebuildRows();
  public void Dispose() => localeSubscription?.Dispose();

  private void RebuildRows() => Rows = pages.Items.Select(total => new AiCostRow(total, localization)).ToArray();
  private void OnStateChanged()
  {
    OnPropertyChanged(nameof(HasMore)); OnPropertyChanged(nameof(HasRows));
    OnPropertyChanged(nameof(ShowsEmptyState)); OnPropertyChanged(nameof(HasContinuationError));
  }
}
