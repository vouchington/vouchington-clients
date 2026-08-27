using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.TopicRecommendations;

public interface ITopicRecommendationDetailService
{
  Task<PostResponse> FetchAsync(
      string recommendationId,
      CancellationToken cancellationToken = default);

  Task VoteAsync(
      string recommendationId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default);

  Task ClearVoteAsync(
      string recommendationId,
      CancellationToken cancellationToken = default);
}
