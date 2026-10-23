namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<TopicClaimPage> FetchMyTopicClaimsAsync(
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimPage>(VouchaApiEndpoints.MyTopicClaims(limit), cancellationToken);

  public Task<TopicClaimPage> FetchTopicClaimsAsync(
      string idOrSlug,
      bool? mineOnly = null,
      bool? activeOnly = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimPage>(
          VouchaApiEndpoints.TopicClaims(idOrSlug, mineOnly, activeOnly, limit),
          cancellationToken);
}
