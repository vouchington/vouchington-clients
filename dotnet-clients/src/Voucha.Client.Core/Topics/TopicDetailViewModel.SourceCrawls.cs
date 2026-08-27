using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private IReadOnlyList<RssFeedCrawl> sourceCrawls = [];
  private string? sourceCrawlsEndCursor;
  private bool hasMoreSourceCrawls;
  private bool isLoadingMoreSourceCrawls;
  private int sourceCrawlsContinuationGuard;
  private int sourceCrawlsGeneration;
  private bool hasLoadedSourceCrawlHistory;
  private string? sourceCrawlsContinuationErrorMessage;
  private RssFeedCrawl? selectedSourceCrawl;

  private VouchaApiClient? ApiClient { get; }

  private bool CanViewPaidSourceCrawlHistory { get; set; }

  private bool CanManageSourceCrawls { get; }

  private bool RequiresAuthoritativeSourceCrawlMembership { get; }

  public IReadOnlyList<RssFeedCrawl> SourceCrawls
  {
    get => sourceCrawls;
    private set
    {
      if (!SetProperty(ref sourceCrawls, value)) return;
      OnPropertyChanged(nameof(HasSourceCrawls));
      OnPropertyChanged(nameof(ShowsSourceCrawlHistoryEmpty));
    }
  }

  public bool HasSourceCrawls => SourceCrawls.Count > 0;

  public bool HasLoadedSourceCrawlHistory => hasLoadedSourceCrawlHistory;

  public bool ShowsSourceCrawlHistoryEmpty => HasLoadedSourceCrawlHistory && !HasSourceCrawls;

  public bool CanViewSourceCrawlHistory => (CanManageSourceCrawls || CanViewPaidSourceCrawlHistory) && CanFollowSource;

  public bool HasMoreSourceCrawls => hasMoreSourceCrawls;

  public string? SourceCrawlsContinuationErrorMessage
  {
    get => sourceCrawlsContinuationErrorMessage;
    private set
    {
      if (!SetProperty(ref sourceCrawlsContinuationErrorMessage, value)) return;
      OnPropertyChanged(nameof(HasSourceCrawlsContinuationError));
    }
  }

  public bool HasSourceCrawlsContinuationError => !string.IsNullOrWhiteSpace(SourceCrawlsContinuationErrorMessage);

  public bool IsLoadingMoreSourceCrawls
  {
    get => isLoadingMoreSourceCrawls;
    private set => SetProperty(ref isLoadingMoreSourceCrawls, value);
  }

  public RssFeedCrawl? SelectedSourceCrawl
  {
    get => selectedSourceCrawl;
    private set
    {
      if (SetProperty(ref selectedSourceCrawl, value)) OnPropertyChanged(nameof(HasSelectedSourceCrawl));
    }
  }

  public bool HasSelectedSourceCrawl => SelectedSourceCrawl is not null;

  private void ApplySourceCrawlsPage(RssFeedCrawlsResponse response, bool append)
  {
    SourceCrawls = append
        ? SourceCrawls.Concat(response.Results).DistinctBy(crawl => crawl.Id).ToArray()
        : response.Results;
    if (!append) SetHasLoadedSourceCrawlHistory(true);
    sourceCrawlsEndCursor = response.PageInfo.EndCursor;
    SourceCrawlsContinuationErrorMessage = null;
    SetHasMoreSourceCrawls(response.PageInfo.HasNextPage);
  }

  private async Task RefreshSourceCrawlEntitlementAsync(CancellationToken cancellationToken)
  {
    if (!RequiresAuthoritativeSourceCrawlMembership || CanManageSourceCrawls || ApiClient is null) return;
    try
    {
      var response = await ApiClient.FetchMembershipAsync(cancellationToken).ConfigureAwait(true);
      SetCanViewPaidSourceCrawlHistory(response.Membership.HasActivePlusOrProMembership());
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      SetCanViewPaidSourceCrawlHistory(false);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      SetCanViewPaidSourceCrawlHistory(false);
    }
  }

  private void SetCanViewPaidSourceCrawlHistory(bool value)
  {
    if (CanViewPaidSourceCrawlHistory == value) return;
    CanViewPaidSourceCrawlHistory = value;
    OnPropertyChanged(nameof(CanViewSourceCrawlHistory));
  }

  private int ResetSourceCrawls()
  {
    var generation = unchecked(++sourceCrawlsGeneration);
    SetHasLoadedSourceCrawlHistory(false);
    SourceCrawls = [];
    SelectedSourceCrawl = null;
    sourceCrawlsEndCursor = null;
    SourceCrawlsContinuationErrorMessage = null;
    ClearSourceCrawlHistoryAccessError();
    ClearSourceCrawlHistoryRequestError();
    SetHasMoreSourceCrawls(false);
    Interlocked.Exchange(ref sourceCrawlsContinuationGuard, 0);
    IsLoadingMoreSourceCrawls = false;
    return generation;
  }

  private void SetHasLoadedSourceCrawlHistory(bool value)
  {
    if (hasLoadedSourceCrawlHistory == value) return;
    hasLoadedSourceCrawlHistory = value;
    OnPropertyChanged(nameof(HasLoadedSourceCrawlHistory));
    OnPropertyChanged(nameof(ShowsSourceCrawlHistoryEmpty));
  }

  private void SetHasMoreSourceCrawls(bool value)
  {
    if (hasMoreSourceCrawls == value) return;
    hasMoreSourceCrawls = value;
    OnPropertyChanged(nameof(HasMoreSourceCrawls));
  }

}
