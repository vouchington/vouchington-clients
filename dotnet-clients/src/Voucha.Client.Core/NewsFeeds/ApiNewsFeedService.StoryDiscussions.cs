using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService
{
  public async Task<StoryDiscussionResult> CreateStoryDiscussionAsync(
      string storyId,
      string fallbackRssFeedItemId,
      CancellationToken cancellationToken = default)
  {
    try
    {
      var result = await client.CreateStoryPostFromStoryAsync(storyId, cancellationToken).ConfigureAwait(false);
      return new StoryDiscussionResult(result.Post.Id);
    }
    catch (VouchaApiException ex) when (StoryDiscussionApiError.IsFeedNotDiscoverable(ex))
    {
      var result = await client.CreateLinkPostFromRssFeedItemAsync(fallbackRssFeedItemId, cancellationToken).ConfigureAwait(false);
      fallbackStoryDiscussionPostIds[storyId] = result.Post.Id;
      return new StoryDiscussionResult(result.Post.Id);
    }
  }
}
