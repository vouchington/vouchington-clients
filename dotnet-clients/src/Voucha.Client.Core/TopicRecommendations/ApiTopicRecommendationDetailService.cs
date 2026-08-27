using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed class ApiTopicRecommendationDetailService(VouchaApiClient client) : ITopicRecommendationDetailService
{
  private readonly VouchaApiClient client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<PostResponse> FetchAsync(
      string recommendationId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<PostResponse>(VouchaApiEndpoints.TopicRecommendation(recommendationId), cancellationToken);

  public Task VoteAsync(
      string recommendationId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      client.VotePostAsync(recommendationId, choice, cancellationToken);

  public Task ClearVoteAsync(
      string recommendationId,
      CancellationToken cancellationToken = default) =>
      client.ClearPostVoteAsync(recommendationId, cancellationToken);
}
