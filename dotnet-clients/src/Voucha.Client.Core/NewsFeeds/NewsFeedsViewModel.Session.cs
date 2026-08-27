namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  public async Task ReloadAfterSessionChangedAsync(
      bool isAuthenticated,
      CancellationToken cancellationToken = default)
  {
    if (newsFeedService is INewsFeedSessionState sessionState)
    {
      sessionState.ResetSessionState();
    }

    if (!isAuthenticated)
    {
      SelectedScope = SelectedScope switch
      {
        NewsFeedScope.YourFeed => NewsFeedScope.AllNews,
        NewsFeedScope.AllNews => NewsFeedScope.AllNews,
        NewsFeedScope.YourSources => NewsFeedScope.AllSources,
        NewsFeedScope.AllSources => NewsFeedScope.AllSources,
        NewsFeedScope.YourPodcasts => NewsFeedScope.AllPodcasts,
        NewsFeedScope.AllPodcasts => NewsFeedScope.AllPodcasts,
        NewsFeedScope.YourPodcastSources => NewsFeedScope.AllPodcastSources,
        NewsFeedScope.AllPodcastSources => NewsFeedScope.AllPodcastSources,
        NewsFeedScope.YourVideos => NewsFeedScope.AllVideos,
        NewsFeedScope.AllVideos => NewsFeedScope.AllVideos,
        NewsFeedScope.YourVideoSources => NewsFeedScope.AllVideoSources,
        NewsFeedScope.AllVideoSources => NewsFeedScope.AllVideoSources,
        _ => SelectedScope,
      };
    }

    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }
}
