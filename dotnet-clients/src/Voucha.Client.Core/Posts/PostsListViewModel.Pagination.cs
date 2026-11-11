using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  private PostRow[] RowsFrom(PostsFeedResponse response) =>
      response.Results
          .Select(result => response.Posts.TryGetValue(result.EntityId ?? result.Id ?? "", out var post)
              ? RowFrom(post, response.PostElections, response.ElectionVotes, response.Bookmarks, response.MarkdownToHtml, response.PostLinkEmbeds)
              : null)
          .Where(post => post is not null)
          .Select(post => post!)
          .ToArray();

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || !HasMore || endCursor is not { } after) return;
    var currentRequest = BeginLoad();
    await ClientLoadAttempt.RunAsync(
        async () =>
        {
          var response = browsing
              ? await postsService.FetchPostsAsync(
                  new FetchPostsRequest(PostTypes: activePostTypes, After: after, TimeRange: activeTimeRange), cancellationToken).ConfigureAwait(true)
              : await postsService.FetchFeedAsync(
                  new FetchPostsFeedRequest(activeFeedType, after, PostTypes: activePostTypes, TimeRange: activeTimeRange), cancellationToken).ConfigureAwait(true);
          CompleteLoad(currentRequest, response, append: true);
        },
        () => CompleteCanceled(currentRequest),
        message => CompleteError(currentRequest, message)).ConfigureAwait(true);
  }
}
