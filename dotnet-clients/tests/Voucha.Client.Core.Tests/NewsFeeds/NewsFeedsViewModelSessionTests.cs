using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelSessionTests
{
  [Theory]
  [InlineData(NewsFeedScope.YourFeed, NewsFeedScope.AllNews)]
  [InlineData(NewsFeedScope.YourSources, NewsFeedScope.AllSources)]
  [InlineData(NewsFeedScope.AllNews, NewsFeedScope.AllNews)]
  [InlineData(NewsFeedScope.AllSources, NewsFeedScope.AllSources)]
  [InlineData(NewsFeedScope.YourPodcasts, NewsFeedScope.AllPodcasts)]
  [InlineData(NewsFeedScope.YourPodcastSources, NewsFeedScope.AllPodcastSources)]
  [InlineData(NewsFeedScope.YourVideos, NewsFeedScope.AllVideos)]
  [InlineData(NewsFeedScope.YourVideoSources, NewsFeedScope.AllVideoSources)]
  public async Task ReloadAfterSessionChangedAsyncSwitchesSignedOutScopes(
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
  public async Task ReloadAfterSessionChangedAsyncKeepsAuthenticatedScopeAndClearsServiceState()
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
