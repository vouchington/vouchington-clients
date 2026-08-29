using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed class ApiTopHashtagsService(VouchaApiClient client) : ITopHashtagsService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<TopHashtagsResponse> FetchAsync(string? query = null, TopHashtagMapping mapping = TopHashtagMapping.All, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchTopHashtagsAsync(query, mapping, after, limit, cancellationToken);

  public Task LinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) =>
      client.LinkTopicAliasAsync(topicId, aliasId, cancellationToken);

  public Task UnlinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) =>
      client.DeleteTopicAliasAsync(topicId, aliasId, cancellationToken);

  public Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default) =>
      client.CreateTopicAsync(request, cancellationToken);
}
