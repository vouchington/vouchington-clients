using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsCoverageTests
{
  [Fact]
  public void ConstructorsAndLabelsReflectFeedKindAndInitialScope()
  {
    var service = new SampleNewsFeedService();
    var defaultViewModel = new NewsFeedsViewModel(service);
    var scopeOverloadViewModel = new NewsFeedsViewModel(service, NewsFeedScope.AllNews);
    var kindOverloadViewModel = new NewsFeedsViewModel(service, NewsFeedKind.Podcasts);
    var explicitViewModel = new NewsFeedsViewModel(service, NewsFeedKind.Videos, NewsFeedScope.AllVideoSources);

    Assert.Equal(NewsFeedKind.News, defaultViewModel.FeedKind);
    Assert.Equal(NewsFeedScope.YourFeed, defaultViewModel.SelectedScope);
    Assert.Equal("News", defaultViewModel.PageTitle);
    Assert.Equal("Your feed", defaultViewModel.PrimaryScopeLabel);
    Assert.Equal("All news", defaultViewModel.AllItemsScopeLabel);
    Assert.Equal("Your sources", defaultViewModel.SourcesScopeLabel);
    Assert.Equal("All sources", defaultViewModel.AllSourcesScopeLabel);

    Assert.Equal(NewsFeedScope.AllNews, scopeOverloadViewModel.SelectedScope);

    Assert.Equal(NewsFeedKind.Podcasts, kindOverloadViewModel.FeedKind);
    Assert.Equal(NewsFeedScope.YourPodcasts, kindOverloadViewModel.SelectedScope);
    Assert.Equal("Podcasts", kindOverloadViewModel.PageTitle);
    Assert.Equal("Your podcasts", kindOverloadViewModel.PrimaryScopeLabel);
    Assert.Equal("All podcasts", kindOverloadViewModel.AllItemsScopeLabel);
    Assert.Equal("Your sources", kindOverloadViewModel.SourcesScopeLabel);
    Assert.Equal("All sources", kindOverloadViewModel.AllSourcesScopeLabel);

    Assert.Equal(NewsFeedKind.Videos, explicitViewModel.FeedKind);
    Assert.Equal(NewsFeedScope.AllVideoSources, explicitViewModel.SelectedScope);
    Assert.Equal("Videos", explicitViewModel.PageTitle);
    Assert.Equal("Your videos", explicitViewModel.PrimaryScopeLabel);
    Assert.Equal("All videos", explicitViewModel.AllItemsScopeLabel);
    Assert.Equal("Your sources", explicitViewModel.SourcesScopeLabel);
    Assert.Equal("All sources", explicitViewModel.AllSourcesScopeLabel);
  }

  [Theory]
  [InlineData(NewsFeedScope.YourFeed, NewsFeedScope.AllNews)]
  [InlineData(NewsFeedScope.AllNews, NewsFeedScope.AllNews)]
  [InlineData(NewsFeedScope.YourSources, NewsFeedScope.AllSources)]
  [InlineData(NewsFeedScope.AllSources, NewsFeedScope.AllSources)]
  [InlineData(NewsFeedScope.YourPodcasts, NewsFeedScope.AllPodcasts)]
  [InlineData(NewsFeedScope.AllPodcasts, NewsFeedScope.AllPodcasts)]
  [InlineData(NewsFeedScope.YourPodcastSources, NewsFeedScope.AllPodcastSources)]
  [InlineData(NewsFeedScope.AllPodcastSources, NewsFeedScope.AllPodcastSources)]
  [InlineData(NewsFeedScope.YourVideos, NewsFeedScope.AllVideos)]
  [InlineData(NewsFeedScope.AllVideos, NewsFeedScope.AllVideos)]
  [InlineData(NewsFeedScope.YourVideoSources, NewsFeedScope.AllVideoSources)]
  [InlineData(NewsFeedScope.AllVideoSources, NewsFeedScope.AllVideoSources)]
  public async Task ReloadAfterSessionChangedAsyncDowngradesSignedOutScopes(
      NewsFeedScope initialScope,
      NewsFeedScope expectedScope)
  {
    var service = new ResettableNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, initialScope);

    await viewModel.ReloadAfterSessionChangedAsync(false, TestContext.Current.CancellationToken);

    Assert.True(service.ResetCalled);
    Assert.Equal(expectedScope, viewModel.SelectedScope);
    Assert.Equal([expectedScope], service.LoadedScopes);
  }

  [Fact]
  public async Task ReloadAfterSessionChangedAsyncLeavesUnknownScopesUntouched()
  {
    var invalidScope = (NewsFeedScope)999;
    var service = new ResettableNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedKind.News, invalidScope);

    await viewModel.ReloadAfterSessionChangedAsync(false, TestContext.Current.CancellationToken);

    Assert.True(service.ResetCalled);
    Assert.Equal(invalidScope, viewModel.SelectedScope);
    Assert.Equal([invalidScope], service.LoadedScopes);
  }

  [Fact]
  public async Task ReloadAfterSessionChangedAsyncClearsSessionStateForAuthenticatedReloads()
  {
    var service = new ResettableNewsFeedService();
    var viewModel = new NewsFeedsViewModel(service, NewsFeedScope.YourSources);

    await viewModel.ReloadAfterSessionChangedAsync(true, TestContext.Current.CancellationToken);

    Assert.True(service.ResetCalled);
    Assert.Equal(NewsFeedScope.YourSources, viewModel.SelectedScope);
    Assert.Equal([NewsFeedScope.YourSources], service.LoadedScopes);
  }

  private sealed class ResettableNewsFeedService : INewsFeedService, INewsFeedSessionState
  {
    public bool ResetCalled { get; private set; }

    public List<NewsFeedScope> LoadedScopes { get; } = [];

    public void ResetSessionState() => ResetCalled = true;

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default)
    {
      LoadedScopes.Add(scope);
      return Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);
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
}
