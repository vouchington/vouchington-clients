namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<TopicClaimPage> FetchMyTopicClaimsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimPage>(VouchaApiEndpoints.MyTopicClaims(after, limit), cancellationToken);

  public Task<TopicClaimPage> FetchTopicClaimsAsync(
      string idOrSlug,
      bool? mineOnly = null,
      bool? activeOnly = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimPage>(
          VouchaApiEndpoints.TopicClaims(idOrSlug, mineOnly, activeOnly, after, limit),
          cancellationToken);
}
