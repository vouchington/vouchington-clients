using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  private PostRow[] RowsFrom(PostsFeedResponse response) =>
      response.Results
          .Select(result => response.Posts.TryGetValue(result.EntityId ?? result.Id ?? "", out var post)
              ? RowFrom(post, response.PostElections, response.ElectionVotes, response.Bookmarks, response.MarkdownToHtml)
              : null)
          .Where(post => post is not null)
          .Select(post => post!)
          .ToArray();

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || !HasMore || endCursor is not { } after) return;
    var currentRequest = BeginLoad();
    try
    {
      var response = browsing
          ? await postsService.FetchPostsAsync(
              new FetchPostsRequest(PostTypes: activePostTypes, After: after), cancellationToken).ConfigureAwait(true)
          : await postsService.FetchFeedAsync(
              new FetchPostsFeedRequest(activeFeedType, after, PostTypes: activePostTypes), cancellationToken).ConfigureAwait(true);
      CompleteLoad(currentRequest, response, append: true);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }
}
