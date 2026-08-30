using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.TopicRecommendations;

public interface ITopHashtagsService
{
  Task<TopHashtagsResponse> FetchAsync(
      string? query = null,
      TopHashtagMapping mapping = TopHashtagMapping.All,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task LinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default);
  Task UnlinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default);
  Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default);
}
