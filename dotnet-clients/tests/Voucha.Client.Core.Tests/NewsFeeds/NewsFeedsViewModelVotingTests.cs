using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelVotingTests
{
  [Fact]
  public async Task VoteRssFeedItemAsyncKeepsTheSameChoiceUntilAnExplicitClear()
  {
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "item-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              VoteScoreNet: 2.25,
              VoteCountUp: 3,
              VoteCountDown: 1),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Single(service.VoteCalls);
    Assert.Equal(("item-1", ElectionVoteChoice.Like), service.VoteCalls[0]);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2, service.VoteCalls.Count);
    Assert.Equal(("item-1", ElectionVoteChoice.Like), service.VoteCalls[0]);
    Assert.Equal(("item-1", ElectionVoteChoice.Like), service.VoteCalls[1]);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], null, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(["item-1"], service.ClearVoteCalls);
  }

  [Fact]
  public async Task VoteRssFeedItemAsyncRollsBackFailedRowAfterLaterOptimisticUpdate()
  {
    var delayedVote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem("item-1", "Article", "News", "Summary", null, DateTimeOffset.UtcNow, VoteScoreNet: 2.25, VoteCountUp: 3, VoteCountDown: 1),
          new NewsFeedItem("item-2", "Article 2", "News", "Summary", null, DateTimeOffset.UtcNow, VoteScoreNet: 2.25, VoteCountUp: 3, VoteCountDown: 1),
        ])
    {
      VoteCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["item-1"] = delayedVote,
      },
    };
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var firstVote = viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.VoteRssFeedItemAsync(viewModel.Items[1], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    delayedVote.SetException(new InvalidOperationException("Mutation failed."));
    await firstVote;

    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2.25, viewModel.Items[1].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task VoteRssFeedItemAsyncRollsBackOnFailure()
  {
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "item-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              VoteScoreNet: 2.25,
              VoteCountUp: 3,
              VoteCountDown: 1),
        ])
    {
      FailVotes = true,
    };
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task VoteRssFeedItemAsyncRollsBackAndRequestsEmailRecovery()
  {
    var service = new RecordingNewsFeedService(
        [new NewsFeedItem("item-1", "Article", "News", "Summary", null, DateTimeOffset.UtcNow, VoteScoreNet: 2.25, VoteCountUp: 3, VoteCountDown: 1)])
    {
      VoteException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task VoteRssFeedItemAsyncRoutesSourceTopicVotesThroughTopicRoute()
  {
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-1",
              VoteScoreNet: 5.5,
              VoteCountUp: 6,
              VoteCountDown: 1),
          new NewsFeedItem(
              "source-2",
              "Source 2",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-1",
              VoteScoreNet: 5.5,
              VoteCountUp: 6,
              VoteCountDown: 1),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);

    Assert.Empty(service.VoteCalls);
    Assert.Equal([("topic-1", ElectionVoteChoice.Dislike)], service.TopicVoteCalls);
    Assert.All(viewModel.Items, item =>
    {
      Assert.Equal(2, item.VoteCountDown);
      Assert.Equal(5.5, item.VoteScoreNet);
    });
  }

  [Fact]
  public async Task VoteRssFeedItemAsyncIgnoresSourceRowsWithoutVoteCounts()
  {
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "source-1",
              "Source",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              NewsFeedItemKind.Source,
              TopicId: "topic-1"),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteRssFeedItemAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Empty(service.VoteCalls);
    Assert.Empty(service.TopicVoteCalls);
  }

  private sealed class RecordingNewsFeedService(IReadOnlyList<NewsFeedItem> items) : INewsFeedService
  {
    public bool FailVotes { get; init; }

    public Exception? VoteException { get; init; }

    public IReadOnlyDictionary<string, TaskCompletionSource>? VoteCompletions { get; init; }

    public List<(string Id, ElectionVoteChoice Choice)> VoteCalls { get; } = [];

    public List<string> ClearVoteCalls { get; } = [];

    public List<(string Id, ElectionVoteChoice Choice)> TopicVoteCalls { get; } = [];

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(items);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteRssFeedItemAsync(string itemId, ElectionVoteChoice choice, CancellationToken cancellationToken = default)
    {
      VoteCalls.Add((itemId, choice));
      if (VoteCompletions?.TryGetValue(itemId, out var completion) == true)
      {
        return completion.Task;
      }

      if (VoteException is not null) return Task.FromException(VoteException);

      return FailVotes
          ? Task.FromException(new InvalidOperationException("Mutation failed."))
          : Task.CompletedTask;
    }

    public Task ClearRssFeedItemVoteAsync(string itemId, CancellationToken cancellationToken = default)
    {
      ClearVoteCalls.Add(itemId);
      return Task.CompletedTask;
    }

    public Task VoteTopicAsync(string topicId, ElectionVoteChoice choice, CancellationToken cancellationToken = default)
    {
      TopicVoteCalls.Add((topicId, choice));
      return FailVotes
          ? Task.FromException(new InvalidOperationException("Mutation failed."))
          : Task.CompletedTask;
    }

    public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
        VoteTopicAsync(topicId, ElectionVoteChoice.Neutral, cancellationToken);
  }
}
