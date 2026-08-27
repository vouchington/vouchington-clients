namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<StoryPostFromStoryResponse> CreateStoryPostFromStoryAsync(
      string storyId,
      CancellationToken cancellationToken = default) =>
      SendAsync<StoryPostFromStoryResponse>(
          VouchaApiEndpoints.CreateStoryPostFromStory(storyId),
          cancellationToken);

  public Task<PostMutationResponse> CreateLinkPostFromRssFeedItemAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.CreateLinkPostFromRssFeedItem(id),
          cancellationToken);
}
