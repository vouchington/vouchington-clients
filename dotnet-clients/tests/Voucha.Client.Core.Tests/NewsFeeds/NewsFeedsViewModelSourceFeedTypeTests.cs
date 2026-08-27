using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelSourceFeedTypeTests
{
  [Theory]
  [InlineData("article", NewsFeedSourceType.Article)]
  [InlineData("podcast", NewsFeedSourceType.Podcast)]
  [InlineData("video", NewsFeedSourceType.Video)]
  public void ParseApiValueReturnsTypedSourceType(string apiValue, NewsFeedSourceType expectedSourceType)
  {
    Assert.Equal(expectedSourceType, NewsFeedSourceTypeExtensions.ParseApiValue(apiValue));
  }

  [Fact]
  public void ParseApiValueReturnsNullForUnknownSourceType()
  {
    Assert.Null(NewsFeedSourceTypeExtensions.ParseApiValue("unknown"));
  }

  [Fact]
  public async Task SourceFeedTypeDefaultsToArticleAndLoadsSourceScopes()
  {
    var service = new RecordingNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedSourceType.Article, viewModel.SelectedSourceFeedType);
    Assert.True(viewModel.IsArticleSourceFeedTypeSelected);
    Assert.False(viewModel.IsPodcastSourceFeedTypeSelected);
    Assert.Equal([(NewsFeedScope.AllSources, NewsFeedSourceType.Article)], service.SourceLoads);
  }

  [Fact]
  public async Task SelectSourceFeedTypeAsyncReloadsCurrentSourceScope()
  {
    var service = new RecordingNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllSources);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Podcast, TestContext.Current.CancellationToken);
    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Video, TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedSourceType.Video, viewModel.SelectedSourceFeedType);
    Assert.False(viewModel.IsPodcastSourceFeedTypeSelected);
    Assert.True(viewModel.IsVideoSourceFeedTypeSelected);
    Assert.Equal([
      (NewsFeedScope.AllSources, NewsFeedSourceType.Article),
      (NewsFeedScope.AllSources, NewsFeedSourceType.Podcast),
      (NewsFeedScope.AllSources, NewsFeedSourceType.Video),
    ], service.SourceLoads);
  }

  [Fact]
  public async Task SelectSourceFeedTypeAsyncDoesNotReloadNonSourceScope()
  {
    var service = new RecordingNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Podcast, TestContext.Current.CancellationToken);

    Assert.Equal(NewsFeedSourceType.Podcast, viewModel.SelectedSourceFeedType);
    Assert.Empty(service.SourceLoads);
    Assert.Equal(1, service.LoadCount);
  }

  [Fact]
  public async Task MediaSourceScopesLockSourceTypeSelectionToFeedKind()
  {
    var service = new RecordingNewsFeedService();
    var viewModel = new NewsFeedsViewModel(
        service,
        NewsFeedKind.Podcasts,
        NewsFeedScope.AllPodcastSources);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Video, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsSourceScopeSelected);
    Assert.True(viewModel.IsAllSourcesSelected);
    Assert.False(viewModel.IsYourSourcesSelected);
    Assert.Equal(NewsFeedSourceType.Podcast, viewModel.SelectedSourceFeedType);
    Assert.Equal([(NewsFeedScope.AllPodcastSources, NewsFeedSourceType.Podcast)], service.SourceLoads);
  }

  [Theory]
  [InlineData(NewsFeedKind.Podcasts, NewsFeedScope.YourPodcasts, NewsFeedScope.AllPodcasts)]
  [InlineData(NewsFeedKind.Videos, NewsFeedScope.YourVideos, NewsFeedScope.AllVideos)]
  public async Task ActiveItemScopeStateUsesFeedKind(
      NewsFeedKind kind,
      NewsFeedScope primaryScope,
      NewsFeedScope allItemsScope)
  {
    var viewModel = new NewsFeedsViewModel(new RecordingNewsFeedService(), kind, primaryScope);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsYourFeedSelected);
    Assert.False(viewModel.IsAllNewsSelected);

    await viewModel.SelectScopeAsync(allItemsScope, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsYourFeedSelected);
    Assert.True(viewModel.IsAllNewsSelected);
  }

  private sealed class RecordingNewsFeedService : INewsFeedService
  {
    public int LoadCount { get; private set; }

    public List<(NewsFeedScope Scope, NewsFeedSourceType SourceType)> SourceLoads { get; } = [];

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default)
    {
      LoadCount++;
      return Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
    }

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default)
    {
      LoadCount++;
      SourceLoads.Add((scope, sourceFeedType));
      return Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
    }

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
}
