namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
      string topicId,
      RssFeedEnabledFilter? enabled = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedsResponse>(
          VouchaApiEndpoints.RssFeedsForTopic(topicId, enabled: enabled),
          cancellationToken);

  public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.FollowTopic(topicId), cancellationToken);

  public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.UnfollowTopic(topicId), cancellationToken);

  public Task<ListResponse<TopicAlias>> FetchTopicAliasesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListResponse<TopicAlias>>(
          VouchaApiEndpoints.TopicAliases(topicId, after, limit),
          cancellationToken);

  public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListResponse<TopicAdditionalHostname>>(
          VouchaApiEndpoints.TopicAdditionalHostnames(topicId, after, limit),
          cancellationToken);

  public Task CreateTopicAliasesAsync(
      string topicId,
      CreateTopicAliasesBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.CreateTopicAliases(topicId, body), cancellationToken);

  public Task DeleteTopicAliasAsync(
      string topicId,
      string alias,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteTopicAlias(topicId, alias), cancellationToken);

  public Task LinkTopicAliasAsync(
      string topicId,
      string aliasId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.LinkTopicAlias(topicId, aliasId), cancellationToken);

  public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(
      string topicId,
      string hostname,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicAdditionalHostnameResponse>(
          VouchaApiEndpoints.CreateTopicAdditionalHostname(topicId, hostname),
          cancellationToken);

  public Task DeleteTopicAdditionalHostnameAsync(
      string topicId,
      string hostnameId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteTopicAdditionalHostname(topicId, hostnameId), cancellationToken);

  public Task<TopicMergeResponse> MergeTopicAliasesAsync(
      string sourceTopicId,
      string destinationIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicMergeResponse>(
          VouchaApiEndpoints.MergeTopicAliases(sourceTopicId, destinationIdOrSlug),
          cancellationToken);
}
