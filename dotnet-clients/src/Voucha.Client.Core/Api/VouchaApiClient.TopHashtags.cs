namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PostsFeedResponse> FetchTopicRecommendationsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostsFeedResponse>(VouchaApiEndpoints.TopicRecommendations(after, limit), cancellationToken);

  public Task<TopHashtagsResponse> FetchTopHashtagsAsync(
      string? query = null,
      TopHashtagMapping mapping = TopHashtagMapping.All,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopHashtagsResponse>(VouchaApiEndpoints.TopHashtags(query, mapping, after, limit), cancellationToken);
}
