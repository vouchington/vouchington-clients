using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Topics;

public interface ITopicsService
{
  Task<TopicSearchResponse> SearchAsync(
      string query,
      CancellationToken cancellationToken = default);

  Task<TopicResponse> FetchTopicAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default);

  Task<TopicMutationResponse> CreateTopicAsync(
      CreateTopicRequest request,
      CancellationToken cancellationToken = default);

  Task<TopicMutationResponse> UpdateTopicAsync(
      string topicIdOrSlug,
      UpdateTopicBody body,
      CancellationToken cancellationToken = default);

  Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
      string topicId,
      RssFeedEnabledFilter? enabled = null,
      CancellationToken cancellationToken = default);

  Task FollowTopicAsync(
      string topicId,
      CancellationToken cancellationToken = default);

  Task UnfollowTopicAsync(
      string topicId,
      CancellationToken cancellationToken = default);

  Task FollowSourceAsync(
      string rssFeedId,
      CancellationToken cancellationToken = default);

  Task UnfollowSourceAsync(
      string rssFeedId,
      CancellationToken cancellationToken = default);

  Task UpdateSourceAsync(
      string rssFeedId,
      UpdateRssFeedBody body,
      CancellationToken cancellationToken = default);

  Task<ListResponse<string>> FetchTopicAliasesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default);

  Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(
      string topicId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default);

  Task CreateTopicAliasesAsync(
      string topicId,
      CreateTopicAliasesBody body,
      CancellationToken cancellationToken = default);

  Task DeleteTopicAliasAsync(
      string topicId,
      string aliasValue,
      CancellationToken cancellationToken = default);

  Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(
      string topicId,
      string hostname,
      CancellationToken cancellationToken = default);

  Task DeleteTopicAdditionalHostnameAsync(
      string topicId,
      string hostnameId,
      CancellationToken cancellationToken = default);

  Task<TopicMergeResponse> MergeTopicAliasesAsync(
      string sourceTopicId,
      string destinationIdOrSlug,
      CancellationToken cancellationToken = default);

  Task VoteTopicAsync(
      string topicId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Semantic topic voting is not implemented by this topics service."));

  Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Clearing a topic vote is not implemented by this topics service."));
}
