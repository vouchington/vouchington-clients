using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel
{
  private string? feedEndCursor;
  private string? feedType;
  private bool hasMoreFeed;

  public bool HasMoreFeed
  {
    get => hasMoreFeed;
    private set => SetProperty(ref hasMoreFeed, value);
  }

  public Task LoadFeedAsync(bool mutual = false, CancellationToken cancellationToken = default) =>
      LoadFeedPageAsync(mutual ? "mutual_follows" : "follow_users", null, false, cancellationToken);

  public Task LoadMoreFeedAsync(CancellationToken cancellationToken = default) =>
      !IsLoading && HasMoreFeed && feedEndCursor is not null && feedType is not null
          ? LoadFeedPageAsync(feedType, feedEndCursor, true, cancellationToken)
          : Task.CompletedTask;

  private void ResetFeedPagination()
  {
    feedEndCursor = null;
    feedType = null;
    HasMoreFeed = false;
  }

  private async Task LoadFeedPageAsync(
      string requestedFeedType,
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var currentRequest = BeginLoad(preserveFeed: append);
    if (!append)
    {
      Items = [];
      LinksEndCursor = null;
      HasMoreLinks = false;
      AnalyticsEndCursor = null;
      HasMoreAnalytics = false;
    }

    try
    {
      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await referralLinksService.FetchFeedAsync(
            new FetchReferralLinksFeedRequest(requestedFeedType, after), cancellationToken).ConfigureAwait(true);
        if (!IsCurrentRequest(currentRequest)) return;
        feedType = requestedFeedType;
        feedEndCursor = response.PageInfo.EndCursor;
        HasMoreFeed = response.PageInfo.HasNextPage && feedEndCursor is not null && feedEndCursor != after;
        if (response.Results.Count == 0 && HasMoreFeed)
        {
          after = feedEndCursor;
          continue;
        }

        var rows = response.Results.Select(RowFromFeed);
        CompleteLoad(currentRequest, (append ? Items.Concat(rows) : rows).DistinctBy(row => row.Id).ToArray());
        return;
      }
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (append || HasMoreFeed) CompleteErrorPreservingRows(currentRequest, ex.Message);
      else CompleteError(currentRequest, ex.Message);
    }
  }
}
