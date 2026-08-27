using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelTests
{
  [Fact]
  public async Task LoadAsyncLoadsYourFeedByDefault()
  {
    var viewModel = new NewsFeedsViewModel(new SampleNewsFeedService());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedScope.YourFeed, viewModel.SelectedScope);
    Assert.True(viewModel.HasItems);
    Assert.All(viewModel.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Title)));
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task SelectScopeAsyncReloadsAllNewsItems()
  {
    var viewModel = new NewsFeedsViewModel(new SampleNewsFeedService());

    await viewModel.SelectScopeAsync(NewsFeedScope.AllNews, TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedScope.AllNews, viewModel.SelectedScope);
    Assert.Equal(4, viewModel.Items.Count);
    Assert.Contains(viewModel.Items, item => item.Id == "all-health-data");
  }

  [Fact]
  public async Task LoadAsyncUsesConfiguredInitialScope()
  {
    var viewModel = new NewsFeedsViewModel(new SampleNewsFeedService(), NewsFeedScope.AllNews);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedScope.AllNews, viewModel.SelectedScope);
    Assert.True(viewModel.IsAllNewsSelected);
    Assert.Equal(4, viewModel.Items.Count);
  }

  [Fact]
  public async Task SelectScopeAsyncSupportsSourceScopes()
  {
    var viewModel = new NewsFeedsViewModel(new SampleNewsFeedService());

    await viewModel.SelectScopeAsync(NewsFeedScope.AllSources, TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedScope.AllSources, viewModel.SelectedScope);
    Assert.True(viewModel.IsAllSourcesSelected);
    Assert.False(viewModel.IsYourSourcesSelected);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task LoadAsyncClearsItemsAndExposesErrorWhenServiceFails()
  {
    var viewModel = new NewsFeedsViewModel(new ThrowingNewsFeedService());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.HasItems);
    Assert.Equal("Feed unavailable.", viewModel.ErrorMessage);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task LoadAsyncClearsItemsAndExposesErrorWhenServiceFailsWithGenericException()
  {
    var viewModel = new NewsFeedsViewModel(new GenericThrowingNewsFeedService());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.HasItems);
    Assert.Equal("Network error.", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task SelectScopeAsyncIgnoresStaleLoadCompletions()
  {
    var newsFeedService = new DeferredNewsFeedService();
    var viewModel = new NewsFeedsViewModel(newsFeedService);

    var allNewsLoad = viewModel.SelectScopeAsync(NewsFeedScope.AllNews, TestContext.Current.CancellationToken);
    var yourFeedLoad = viewModel.SelectScopeAsync(NewsFeedScope.YourFeed, TestContext.Current.CancellationToken);

    Assert.Equal(2, newsFeedService.Calls.Count);

    newsFeedService.Calls[1].Completion.SetResult(
        [new NewsFeedItem("your-feed", "Your feed", "Current source", "Current summary", null, DateTimeOffset.UtcNow)]);
    await yourFeedLoad.ConfigureAwait(true);

    newsFeedService.Calls[0].Completion.SetResult(
        [new NewsFeedItem("all-news", "All news", "Stale source", "Stale summary", null, DateTimeOffset.UtcNow)]);
    await allNewsLoad.ConfigureAwait(true);

    Assert.Equal(NewsFeedScope.YourFeed, viewModel.SelectedScope);
    Assert.True(viewModel.IsYourFeedSelected);
    Assert.False(viewModel.IsAllNewsSelected);
    Assert.Collection(viewModel.Items, item => Assert.Equal("your-feed", item.Id));
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ToggleSourceFollowAsyncOptimisticallyUpdatesWithoutReloading()
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
              NewsFeedItemKind.Source),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSourceFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsFollowingSource);
    Assert.Equal([("source-1", true)], service.SourceMutations);
    Assert.Equal(1, service.LoadCount);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleSourceFollowAsyncRemovesYourSourceAndRollsBackOnFailure()
  {
    var source = new NewsFeedItem(
        "source-1",
        "Source",
        "News",
        "Summary",
        null,
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Source,
        IsFollowingSource: true);
    var service = new RecordingNewsFeedService([source]) { FailSourceMutations = true };
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.YourSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleSourceFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Collection(viewModel.Items, item => Assert.Equal("source-1", item.Id));
    Assert.True(viewModel.Items[0].IsFollowingSource);
    Assert.True(viewModel.HasError);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleTopicFollowAsyncOptimisticallyUpdatesMatchingSources()
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

    await viewModel.ToggleTopicFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsFollowingTopic);
    Assert.Equal([("topic-1", true)], service.TopicMutations);
    Assert.Equal(1, service.LoadCount);
  }

  [Fact]
  public async Task ToggleSourceFollowFailureDoesNotRollbackAfterNewLoad()
  {
    var service = new DeferredMutationNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var toggle = viewModel.ToggleSourceFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    Assert.True(viewModel.Items[0].IsFollowingSource);

    await viewModel.SelectScopeAsync(NewsFeedScope.YourFeed, TestContext.Current.CancellationToken);
    service.SourceMutation.SetException(new InvalidOperationException("Mutation failed."));
    await toggle.ConfigureAwait(true);

    Assert.Equal(NewsFeedScope.YourFeed, viewModel.SelectedScope);
    Assert.Collection(viewModel.Items, item => Assert.Equal("feed-1", item.Id));
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleTopicFollowFailureDoesNotRollbackAfterNewLoad()
  {
    var service = new DeferredMutationNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var toggle = viewModel.ToggleTopicFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    Assert.True(viewModel.Items[0].IsFollowingTopic);

    await viewModel.SelectScopeAsync(NewsFeedScope.YourFeed, TestContext.Current.CancellationToken);
    service.TopicMutation.SetException(new InvalidOperationException("Mutation failed."));
    await toggle.ConfigureAwait(true);

    Assert.Equal(NewsFeedScope.YourFeed, viewModel.SelectedScope);
    Assert.Collection(viewModel.Items, item => Assert.Equal("feed-1", item.Id));
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleReadAsyncOptimisticallyUpdatesWithoutReloading()
  {
    var service = new RecordingNewsFeedService(
        [
          new NewsFeedItem(
              "article-1",
              "Article",
              "News",
              "Summary",
              null,
              DateTimeOffset.UtcNow),
        ]);
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ToggleReadAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsRead);
    Assert.Equal([("article-1", true)], service.ReadMutations);
    Assert.Equal(1, service.LoadCount);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleMutationFailuresSetErrorMessage()
  {
    var viewModel = new NewsFeedsViewModel(new MutatingThrowingNewsFeedService());
    var item = new NewsFeedItem(
        "source-1",
        "Source",
        "News",
        "Summary",
        null,
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Source);

    await viewModel.ToggleSourceFollowAsync(item, TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  private sealed class RecordingNewsFeedService(IReadOnlyList<NewsFeedItem> items) : INewsFeedService
  {
    public int LoadCount { get; private set; }

    public bool FailSourceMutations { get; init; }

    public List<(string Id, bool Following)> SourceMutations { get; } = [];

    public List<(string Id, bool Following)> TopicMutations { get; } = [];

    public List<(string Id, bool Read)> ReadMutations { get; } = [];

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default)
    {
      LoadCount++;
      return Task.FromResult(items);
    }

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default)
    {
      LoadCount++;
      return Task.FromResult(items);
    }

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default)
    {
      SourceMutations.Add((sourceId, following));
      return FailSourceMutations
          ? Task.FromException(new InvalidOperationException("Mutation failed."))
          : Task.CompletedTask;
    }

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default)
    {
      TopicMutations.Add((topicId, following));
      return Task.CompletedTask;
    }

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default)
    {
      ReadMutations.Add((itemId, read));
      return Task.CompletedTask;
    }

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DeferredMutationNewsFeedService : INewsFeedService
  {
    private static readonly NewsFeedItem SourceItem = new(
        "source-1",
        "Source",
        "News",
        "Summary",
        null,
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Source,
        TopicId: "topic-1");

    private static readonly NewsFeedItem FeedItem = new(
        "feed-1",
        "Feed",
        "News",
        "Summary",
        null,
        DateTimeOffset.UtcNow);

    public TaskCompletionSource SourceMutation { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource TopicMutation { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>(
            scope == NewsFeedScope.AllSources ? [SourceItem] : [FeedItem]);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        SourceMutation.Task;

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        TopicMutation.Task;

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ThrowingNewsFeedService : INewsFeedService
  {
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<NewsFeedItem>>(
            new InvalidOperationException("Feed unavailable."));

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

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class GenericThrowingNewsFeedService : INewsFeedService
  {
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<NewsFeedItem>>(
            new HttpRequestException("Network error."));

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

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class MutatingThrowingNewsFeedService : INewsFeedService
  {
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Mutation failed."));

    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Mutation failed."));

    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Mutation failed."));

    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Mutation failed."));

    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Mutation failed."));
  }

  private sealed class DeferredNewsFeedService : INewsFeedService
  {
    public List<DeferredNewsFeedCall> Calls { get; } = [];

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default)
    {
      var call = new DeferredNewsFeedCall(
          scope,
          new TaskCompletionSource<IReadOnlyList<NewsFeedItem>>(TaskCreationOptions.RunContinuationsAsynchronously));
      Calls.Add(call);
      return call.Completion.Task;
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
  }

  private sealed record DeferredNewsFeedCall(
      NewsFeedScope Scope,
      TaskCompletionSource<IReadOnlyList<NewsFeedItem>> Completion);
}
