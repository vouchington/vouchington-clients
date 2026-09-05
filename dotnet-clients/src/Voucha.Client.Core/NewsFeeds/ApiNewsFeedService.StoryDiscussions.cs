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
      var storyIntent = $"story\u001f{storyId}";
      var storyScope = $"story-discussion\u001f{storyId}";
      var result = await client.CreateStoryPostFromStoryAsync(
          storyId, contributionIdentity.KeyFor(storyScope, storyIntent), cancellationToken).ConfigureAwait(false);
      contributionIdentity.Complete(storyScope, storyIntent);
      return new StoryDiscussionResult(result.Post.Id);
    }
    catch (VouchaApiException ex) when (StoryDiscussionApiError.IsFeedNotDiscoverable(ex))
    {
      var rssIntent = $"rss\u001f{fallbackRssFeedItemId}";
      var rssScope = $"rss-discussion\u001f{fallbackRssFeedItemId}";
      var result = await client.CreateLinkPostFromRssFeedItemAsync(
          fallbackRssFeedItemId, contributionIdentity.KeyFor(rssScope, rssIntent), cancellationToken).ConfigureAwait(false);
      contributionIdentity.Complete(rssScope, rssIntent);
      fallbackStoryDiscussionPostIds[storyId] = result.Post.Id;
      return new StoryDiscussionResult(result.Post.Id);
    }
  }
}
