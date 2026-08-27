using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicsViewModelTests
{
  [Fact]
  public async Task SearchAsyncBuildsTopicRows()
  {
    var service = new RecordingTopicsService
    {
      SearchResponse = new TopicSearchResponse(
          [new EntityReference("topic", "topic-1", "topic-1", null, null, null, null, null, null, null, null)],
          new PageInfo(null, false, null),
          new Dictionary<string, Topic>(StringComparer.Ordinal)
          {
            ["topic-1"] = new Topic("topic-1", "Rewards", "rewards", "topic", "Reward cards"),
          },
          new Dictionary<string, object>(StringComparer.Ordinal)),
    };
    var viewModel = new TopicsViewModel(service);

    await viewModel.SearchAsync("reward", TestContext.Current.CancellationToken);

    Assert.Equal("reward", service.LastQuery);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("topic-1", item.Id);
      Assert.Equal("Rewards", item.Name);
      Assert.Equal("Reward cards", item.Description);
    });
  }

  [Fact]
  public async Task LoadTopicAsyncSetsSelectedTopic()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(new Topic("topic-2", "Card", "card", "card")),
    };
    var viewModel = new TopicsViewModel(service);

    await viewModel.LoadTopicAsync("card", TestContext.Current.CancellationToken);

    Assert.Equal("card", service.LastTopicId);
    Assert.Equal("topic-2", viewModel.SelectedTopic?.Id);
    Assert.Collection(viewModel.Items, item => Assert.Equal("Card", item.Name));
  }

  [Fact]
  public async Task LoadAsyncUsesSearchQueryAndCancellationReturnsIdle()
  {
    var service = new RecordingTopicsService
    {
      SearchResponse = new TopicSearchResponse(
          [],
          new PageInfo(null, false, null),
          new Dictionary<string, Topic>(StringComparer.Ordinal),
          new Dictionary<string, object>(StringComparer.Ordinal)),
    };
    var viewModel = new TopicsViewModel(service)
    {
      SearchQuery = "cards",
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("cards", service.LastQuery);
    Assert.False(viewModel.HasItems);

    service.CancelSearch = true;
    await viewModel.SearchAsync("cancel", TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public async Task VoteTopicAsyncKeepsTheSameChoiceUntilAnExplicitClear()
  {
    var service = new RecordingTopicsService
    {
      SearchResponse = new TopicSearchResponse(
          [new EntityReference("topic", "topic-1", "topic-1", null, null, null, null, null, null, null, null)],
          new PageInfo(null, false, null),
          new Dictionary<string, Topic>(StringComparer.Ordinal)
          {
            ["topic-1"] = new Topic("topic-1", "Rewards", "rewards", "topic", "Reward cards"),
          },
          new Dictionary<string, object>(StringComparer.Ordinal),
          new Dictionary<string, TopicElection>(StringComparer.Ordinal)
          {
            ["topic-1"] = new TopicElection("topic_election", "topic-1", 2.25, 3, 1),
          }),
    };
    var viewModel = new TopicsViewModel(service);

    await viewModel.SearchAsync("reward", TestContext.Current.CancellationToken);
    await viewModel.VoteTopicAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Single(service.VoteCalls);
    Assert.Equal(("topic-1", ElectionVoteChoice.Like), service.VoteCalls[0]);

    await viewModel.VoteTopicAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2, service.VoteCalls.Count);
    Assert.Equal(("topic-1", ElectionVoteChoice.Like), service.VoteCalls[0]);
    Assert.Equal(("topic-1", ElectionVoteChoice.Like), service.VoteCalls[1]);

    await viewModel.VoteTopicAsync(viewModel.Items[0], null, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(["topic-1"], service.ClearVoteCalls);
  }

  [Fact]
  public async Task VoteTopicAsyncRollsBackFailedRowAfterLaterOptimisticUpdate()
  {
    var delayedVote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingTopicsService
    {
      SearchResponse = new TopicSearchResponse(
          [
            new EntityReference("topic", "topic-1", "topic-1", null, null, null, null, null, null, null, null),
            new EntityReference("topic", "topic-2", "topic-2", null, null, null, null, null, null, null, null),
          ],
          new PageInfo(null, false, null),
          new Dictionary<string, Topic>(StringComparer.Ordinal)
          {
            ["topic-1"] = new Topic("topic-1", "Rewards", "rewards", "topic"),
            ["topic-2"] = new Topic("topic-2", "Cards", "cards", "topic"),
          },
          new Dictionary<string, object>(StringComparer.Ordinal),
          new Dictionary<string, TopicElection>(StringComparer.Ordinal)
          {
            ["topic-1"] = new TopicElection("topic_election", "topic-1", 2.25, 3, 1),
            ["topic-2"] = new TopicElection("topic_election", "topic-2", 2.25, 3, 1),
          }),
      VoteCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["topic-1"] = delayedVote,
      },
    };
    var viewModel = new TopicsViewModel(service);
    await viewModel.SearchAsync("", TestContext.Current.CancellationToken);

    var firstVote = viewModel.VoteTopicAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.VoteTopicAsync(viewModel.Items[1], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    delayedVote.SetException(new InvalidOperationException("Mutation failed."));
    await firstVote;

    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2.25, viewModel.Items[1].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task VoteTopicAsyncRollsBackOnFailure()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-2", "Card", "card", "topic"),
          TopicElection: new TopicElection("topic_election", "topic-2", 2.25, 3, 1)),
      FailVotes = true,
    };
    var viewModel = new TopicsViewModel(service);

    await viewModel.LoadTopicAsync("card", TestContext.Current.CancellationToken);
    await viewModel.VoteTopicAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task VoteTopicAsyncRestoresSelectedTopicAndRequestsEmailRecovery()
  {
    var service = new RecordingTopicsService
    {
      TopicResponse = new TopicResponse(
          new Topic("topic-2", "Card", "card", "topic"),
          TopicElection: new TopicElection("topic_election", "topic-2", 2.25, 3, 1)),
      VoteException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new TopicsViewModel(service);
    await viewModel.LoadTopicAsync("card", TestContext.Current.CancellationToken);

    await viewModel.VoteTopicAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.SelectedTopic?.VoteCountUp);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  private sealed class RecordingTopicsService : ITopicsService
  {
    public string? LastQuery { get; private set; }

    public string? LastTopicId { get; private set; }

    public TopicSearchResponse? SearchResponse { get; init; }

    public TopicResponse? TopicResponse { get; init; }

    public bool CancelSearch { get; set; }

    public bool FailVotes { get; init; }

    public Exception? VoteException { get; init; }

    public IReadOnlyDictionary<string, TaskCompletionSource>? VoteCompletions { get; init; }

    public List<(string Id, ElectionVoteChoice Choice)> VoteCalls { get; } = [];

    public List<string> ClearVoteCalls { get; } = [];

    public Task<TopicSearchResponse> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
      LastQuery = query;
      if (CancelSearch)
      {
        return Task.FromCanceled<TopicSearchResponse>(new CancellationToken(canceled: true));
      }

      return Task.FromResult(SearchResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<TopicResponse> FetchTopicAsync(
        string topicIdOrSlug,
        CancellationToken cancellationToken = default)
    {
      LastTopicId = topicIdOrSlug;
      return Task.FromResult(TopicResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<TopicMutationResponse> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TopicMutationResponse> UpdateTopicAsync(
        string topicIdOrSlug,
        UpdateTopicBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(
        string topicId,
        RssFeedEnabledFilter? enabled = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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

    public Task DeleteTopicAliasAsync(string topicId, string alias, CancellationToken cancellationToken = default) =>
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

    public Task VoteTopicAsync(
        string topicId,
        ElectionVoteChoice choice,
        CancellationToken cancellationToken = default)
    {
      VoteCalls.Add((topicId, choice));
      if (VoteCompletions?.TryGetValue(topicId, out var completion) == true)
      {
        return completion.Task;
      }

      if (VoteException is not null) return Task.FromException(VoteException);

      return FailVotes
          ? Task.FromException(new InvalidOperationException("Mutation failed."))
          : Task.CompletedTask;
    }

    public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default)
    {
      ClearVoteCalls.Add(topicId);
      return Task.CompletedTask;
    }
  }
}
