using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Topics;

public sealed class ApiTopicsService : ITopicsService
{
  private readonly VouchaApiClient client;

  public ApiTopicsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<TopicSearchResponse> SearchAsync(
      string query,
      CancellationToken cancellationToken = default) =>
      client.SearchTopicsAsync(new SearchTopicsRequest(query), cancellationToken);

  public Task<TopicResponse> FetchTopicAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchTopicAsync(topicIdOrSlug, cancellationToken);

  public Task<TopicMutationResponse> CreateTopicAsync(
      CreateTopicRequest request,
      CancellationToken cancellationToken = default) =>
      client.CreateTopicAsync(request, cancellationToken);

  public Task<TopicMutationResponse> UpdateTopicAsync(
      string topicIdOrSlug,
      UpdateTopicBody body,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<TopicMutationResponse>(
          VouchaApiEndpoints.UpdateTopic(topicIdOrSlug, body),
          cancellationToken);

  public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
      string topicId,
      RssFeedEnabledFilter? enabled = null,
      CancellationToken cancellationToken = default) =>
      client.FetchRssFeedsForTopicAsync(topicId, enabled, cancellationToken);

  public Task FollowTopicAsync(
      string topicId,
      CancellationToken cancellationToken = default) =>
      client.FollowTopicAsync(topicId, cancellationToken);

  public Task UnfollowTopicAsync(
      string topicId,
      CancellationToken cancellationToken = default) =>
      client.UnfollowTopicAsync(topicId, cancellationToken);

  public Task FollowSourceAsync(
      string rssFeedId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.FollowRssFeed(rssFeedId), cancellationToken);

  public Task UnfollowSourceAsync(
      string rssFeedId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.UnfollowRssFeed(rssFeedId), cancellationToken);

  public Task UpdateSourceAsync(
      string rssFeedId,
      UpdateRssFeedBody body,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.UpdateRssFeed(rssFeedId, body), cancellationToken);

  public Task<ListResponse<string>> FetchTopicAliasesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      client.FetchTopicAliasesAsync(topicId, after, limit, cancellationToken);

  public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      client.FetchTopicAdditionalHostnamesAsync(topicId, after, limit, cancellationToken);

  public Task CreateTopicAliasesAsync(
      string topicId,
      CreateTopicAliasesBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateTopicAliasesAsync(topicId, body, cancellationToken);

  public Task DeleteTopicAliasAsync(
      string topicId,
      string aliasValue,
      CancellationToken cancellationToken = default) =>
      client.DeleteTopicAliasAsync(topicId, aliasValue, cancellationToken);

  public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(
      string topicId,
      string hostname,
      CancellationToken cancellationToken = default) =>
      client.CreateTopicAdditionalHostnameAsync(topicId, hostname, cancellationToken);

  public Task DeleteTopicAdditionalHostnameAsync(
      string topicId,
      string hostnameId,
      CancellationToken cancellationToken = default) =>
      client.DeleteTopicAdditionalHostnameAsync(topicId, hostnameId, cancellationToken);

  public Task<TopicMergeResponse> MergeTopicAliasesAsync(
      string sourceTopicId,
      string destinationIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.MergeTopicAliasesAsync(sourceTopicId, destinationIdOrSlug, cancellationToken);

  public Task VoteTopicAsync(
      string topicId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      client.VoteTopicAsync(topicId, choice, cancellationToken);

  public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      client.ClearTopicVoteAsync(topicId, cancellationToken);
}
