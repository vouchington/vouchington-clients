namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<StoryPostFromStoryResponse> CreateStoryPostFromStoryAsync(
      string storyId,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      SendAsync<StoryPostFromStoryResponse>(
          VouchaApiEndpoints.CreateStoryPostFromStory(storyId, idempotencyKey), cancellationToken);

  public Task<PostMutationResponse> CreateLinkPostFromRssFeedItemAsync(
      string id,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.CreateLinkPostFromRssFeedItem(id, idempotencyKey), cancellationToken);
}
