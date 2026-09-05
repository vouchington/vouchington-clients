using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelStoryDiscussionTests
{
  [Fact]
  public async Task StartStoryDiscussionAsyncMarksStoryRowsDiscussed()
  {
    var service = new StoryDiscussionNewsFeedService(
        [
            new NewsFeedItem(
                "item-1",
                "Article",
                "News",
                "Summary",
                null,
                DateTimeOffset.UtcNow,
                StoryId: "story-1",
                StoryPeerCount: 1),
            new NewsFeedItem(
                "item-2",
                "Peer",
                "News",
                "Summary",
                null,
                DateTimeOffset.UtcNow,
                StoryId: "story-1",
                StoryPeerCount: 1),
        ]);
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.StartStoryDiscussionAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Equal([("story-1", "item-1")], service.StoryDiscussionCalls);
    Assert.NotNull(result);
    Assert.Equal("post-created", result!.PostId);
    Assert.All(viewModel.Items, item =>
    {
      Assert.Equal("post-created", item.StoryPostId);
      Assert.False(item.CanStartStoryDiscussion);
      Assert.False(item.IsStartingStoryDiscussion);
    });
  }

  [Fact]
  public async Task StartStoryDiscussionAsyncBlocksDuplicateStorySubmission()
  {
    var service = new StoryDiscussionNewsFeedService(
        [
            new NewsFeedItem(
                "item-1",
                "Article",
                "News",
                "Summary",
                null,
                DateTimeOffset.UtcNow,
                StoryId: "story-1",
                StoryPeerCount: 1),
            new NewsFeedItem(
                "item-2",
                "Peer",
                "News",
                "Summary",
                null,
                DateTimeOffset.UtcNow,
                StoryId: "story-1",
                StoryPeerCount: 1),
        ])
    {
      PendingResult = new TaskCompletionSource<StoryDiscussionResult>(TaskCreationOptions.RunContinuationsAsynchronously)
    };
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var pending = viewModel.StartStoryDiscussionAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await Task.Yield();

    Assert.All(viewModel.Items, item => Assert.True(item.IsStartingStoryDiscussion));
    Assert.Null(await viewModel.StartStoryDiscussionAsync(viewModel.Items[1], TestContext.Current.CancellationToken));
    Assert.Equal([("story-1", "item-1")], service.StoryDiscussionCalls);

    service.PendingResult.SetResult(new StoryDiscussionResult("post-created"));
    Assert.Equal("post-created", (await pending)?.PostId);
  }

  [Fact]
  public async Task StartStoryDiscussionAsyncReloadsWhenStoryAlreadyHasPost()
  {
    var service = new ConflictStoryDiscussionNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.StartStoryDiscussionAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Equal(2, service.LoadCount);
    Assert.Equal([("story-1", "item-1")], service.StoryDiscussionCalls);
    Assert.NotNull(result);
    Assert.Equal("post-existing", result!.PostId);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("post-existing", item.StoryPostId);
      Assert.False(item.CanStartStoryDiscussion);
      Assert.False(item.IsStartingStoryDiscussion);
    });
  }

  [Fact]
  public async Task StartStoryDiscussionAsyncClearsPendingStateAndRequestsEmailRecovery()
  {
    var service = new StoryDiscussionNewsFeedService(
        [new NewsFeedItem("item-1", "Article", "News", "Summary", null, DateTimeOffset.UtcNow, StoryId: "story-1", StoryPeerCount: 1)])
    {
      Failure = new VouchaApiException(
          HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.StartStoryDiscussionAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Null(result);
    Assert.False(viewModel.Items[0].IsStartingStoryDiscussion);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Theory]
  [InlineData(HttpStatusCode.Conflict, "CONTRIBUTION_ADMISSION_IN_PROGRESS", "Your post is still being sent. Try again shortly.")]
  [InlineData(HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "We couldn't match this draft to the earlier request. Try again.")]
  [InlineData(HttpStatusCode.TooManyRequests, "CONTRIBUTION_QUOTA_EXCEEDED", "You can't post right now. Try again later.")]
  public async Task StartStoryDiscussionAsyncKeepsTheDraftOnAdmissionConflicts(
      HttpStatusCode statusCode,
      string code,
      string expectedMessage)
  {
    var service = new StoryDiscussionNewsFeedService(
        [new NewsFeedItem("item-1", "Article", "News", "Summary", null, DateTimeOffset.UtcNow, StoryId: "story-1", StoryPeerCount: 1)])
    {
      Failure = new VouchaApiException(statusCode, $$"""{"code":"{{code}}"}"""),
    };
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.StartStoryDiscussionAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Null(result);
    Assert.Equal(expectedMessage, viewModel.ErrorMessage);
    Assert.False(viewModel.Items[0].IsStartingStoryDiscussion);
    Assert.True(viewModel.Items[0].CanStartStoryDiscussion);
  }

  private sealed class StoryDiscussionNewsFeedService(
      IReadOnlyList<NewsFeedItem> items) : INewsFeedService, IStoryDiscussionService
  {
    public List<(string StoryId, string FallbackRssFeedItemId)> StoryDiscussionCalls { get; } = [];

    public TaskCompletionSource<StoryDiscussionResult>? PendingResult { get; init; }

    public Exception? Failure { get; init; }

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

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<StoryDiscussionResult> CreateStoryDiscussionAsync(
        string storyId,
        string fallbackRssFeedItemId,
        CancellationToken cancellationToken = default)
    {
      StoryDiscussionCalls.Add((storyId, fallbackRssFeedItemId));
      if (Failure is not null) return Task.FromException<StoryDiscussionResult>(Failure);
      return PendingResult?.Task ?? Task.FromResult(new StoryDiscussionResult("post-created"));
    }
  }

  private sealed class ConflictStoryDiscussionNewsFeedService : INewsFeedService, IStoryDiscussionService
  {
    public int LoadCount { get; private set; }
    public List<(string StoryId, string FallbackRssFeedItemId)> StoryDiscussionCalls { get; } = [];

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default)
    {
      LoadCount++;
      var storyPostId = LoadCount > 1 ? "post-existing" : null;
      return Task.FromResult<IReadOnlyList<NewsFeedItem>>([
          new NewsFeedItem(
              "item-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow,
              StoryId: "story-1",
              StoryPeerCount: 1,
              StoryPostId: storyPostId),
      ]);
    }

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

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<StoryDiscussionResult> CreateStoryDiscussionAsync(
        string storyId,
        string fallbackRssFeedItemId,
        CancellationToken cancellationToken = default)
    {
      StoryDiscussionCalls.Add((storyId, fallbackRssFeedItemId));
      return Task.FromException<StoryDiscussionResult>(new VouchaApiException(HttpStatusCode.Conflict, "{}"));
    }
  }
}
