using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve the rendered crawl list and expose a retry state.")]
  public async Task LoadSourceCrawlsAsync(CancellationToken cancellationToken = default)
  {
    await RefreshSourceCrawlEntitlementAsync(cancellationToken).ConfigureAwait(true);
    if (!CanViewSourceCrawlHistory)
    {
      ResetSourceCrawls();
      SetSourceCrawlHistoryAccessError();
      return;
    }
    if (ApiClient is null || string.IsNullOrWhiteSpace(SourceRssFeedId)) return;
    var generation = ResetSourceCrawls();
    try
    {
      var response = await ApiClient.FetchRssFeedCrawlsAsync(SourceRssFeedId, cancellationToken: cancellationToken)
          .ConfigureAwait(true);
      if (generation == sourceCrawlsGeneration) ApplySourceCrawlsPage(response, append: false);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (generation == sourceCrawlsGeneration) SetSourceCrawlHistoryRequestError(null);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Crawl detail failures expose a localized retry state.")]
  public async Task LoadSourceCrawlAsync(string crawlId, CancellationToken cancellationToken = default)
  {
    var generation = ResetSourceCrawls();
    await RefreshSourceCrawlEntitlementAsync(cancellationToken).ConfigureAwait(true);
    if (!CanViewSourceCrawlHistory)
    {
      SetSourceCrawlHistoryAccessError();
      return;
    }
    if (ApiClient is null || SourceRssFeedId is null || string.IsNullOrWhiteSpace(crawlId)) return;
    try
    {
      var response = await ApiClient.FetchRssFeedCrawlAsync(SourceRssFeedId, crawlId, cancellationToken)
          .ConfigureAwait(true);
      if (generation == sourceCrawlsGeneration) SelectedSourceCrawl = response.Crawl;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (generation == sourceCrawlsGeneration) SetSourceCrawlHistoryRequestError(crawlId);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve the rendered crawl list and expose a retry state.")]
  public async Task LoadMoreSourceCrawlsAsync(CancellationToken cancellationToken = default)
  {
    await RefreshSourceCrawlEntitlementAsync(cancellationToken).ConfigureAwait(true);
    if (!CanViewSourceCrawlHistory)
    {
      ResetSourceCrawls();
      SetSourceCrawlHistoryAccessError();
      return;
    }
    if (ApiClient is null || SourceRssFeedId is null || !HasMoreSourceCrawls || sourceCrawlsEndCursor is null ||
        Interlocked.CompareExchange(ref sourceCrawlsContinuationGuard, 1, 0) != 0) return;
    var generation = sourceCrawlsGeneration;
    IsLoadingMoreSourceCrawls = true;
    SourceCrawlsContinuationErrorMessage = null;
    try
    {
      var response = await ApiClient.FetchRssFeedCrawlsAsync(SourceRssFeedId, sourceCrawlsEndCursor, cancellationToken: cancellationToken)
          .ConfigureAwait(true);
      if (generation == sourceCrawlsGeneration) ApplySourceCrawlsPage(response, append: true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      if (generation == sourceCrawlsGeneration) SourceCrawlsContinuationErrorMessage = ex.Message;
    }
    finally
    {
      Interlocked.Exchange(ref sourceCrawlsContinuationGuard, 0);
      IsLoadingMoreSourceCrawls = false;
    }
  }
}
