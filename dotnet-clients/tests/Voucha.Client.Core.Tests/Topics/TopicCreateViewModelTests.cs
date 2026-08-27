using Voucha.Client.Core.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicCreateViewModelTests
{
  [Fact]
  public async Task CreateAsyncSubmitsCreateTopicRequest()
  {
    var service = new RecordingTopicsService
    {
      CreateResponse = new TopicMutationResponse(new Topic("topic-1", "Rewards", "rewards", "topic")),
    };
    var viewModel = new TopicCreateViewModel(service)
    {
      Name = "Rewards",
      Slug = "rewards",
      TopicType = "topic",
      Markdown = "Body",
    };

    var created = await viewModel.CreateAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Rewards", service.LastCreateRequest?.Name);
    Assert.Equal("rewards", service.LastCreateRequest?.Slug);
    Assert.Equal("topic", service.LastCreateRequest?.TopicType);
    Assert.Equal("topic-1", created.Topic.Id);
  }

  private sealed class RecordingTopicsService : ITopicsService
  {
    public CreateTopicRequest? LastCreateRequest { get; private set; }

    public TopicMutationResponse? CreateResponse { get; init; }

    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicResponse> FetchTopicAsync(
        string topicIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default)
    {
      LastCreateRequest = request;
      return Task.FromResult(CreateResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<TopicMutationResponse> UpdateTopicAsync(
        string topicIdOrSlug,
        UpdateTopicBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VoteTopicAsync(
        string topicId,
        int score,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UpdateSourceAsync(
        string rssFeedId,
        UpdateRssFeedBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<string>> FetchTopicAliasesAsync(
        string topicId,
        string? after = null,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(
        string topicId,
        string? after = null,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CreateTopicAliasesAsync(
        string topicId,
        CreateTopicAliasesBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAliasAsync(string topicId, string aliasValue, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(
        string topicId,
        string hostname,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteTopicAdditionalHostnameAsync(
        string topicId,
        string hostnameId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMergeResponse> MergeTopicAliasesAsync(
        string sourceTopicId,
        string destinationIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
