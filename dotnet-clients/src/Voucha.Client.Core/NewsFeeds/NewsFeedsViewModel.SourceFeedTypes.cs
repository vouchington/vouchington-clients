namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private NewsFeedSourceType selectedSourceFeedType = NewsFeedSourceType.Article;

  public NewsFeedSourceType SelectedSourceFeedType
  {
    get => selectedSourceFeedType;
    private set
    {
      if (selectedSourceFeedType == value) return;
      selectedSourceFeedType = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsArticleSourceFeedTypeSelected));
      OnPropertyChanged(nameof(IsPodcastSourceFeedTypeSelected));
      OnPropertyChanged(nameof(IsVideoSourceFeedTypeSelected));
    }
  }

  public bool IsSourceScopeSelected => FeedKind == NewsFeedKind.News && SelectedScope.IsSourcesScope();

  public bool IsArticleSourceFeedTypeSelected => SelectedSourceFeedType == NewsFeedSourceType.Article;

  public bool IsPodcastSourceFeedTypeSelected => SelectedSourceFeedType == NewsFeedSourceType.Podcast;

  public bool IsVideoSourceFeedTypeSelected => SelectedSourceFeedType == NewsFeedSourceType.Video;

  public async Task SelectSourceFeedTypeAsync(
      NewsFeedSourceType sourceFeedType,
      CancellationToken cancellationToken = default)
  {
    if (FeedKind != NewsFeedKind.News)
    {
      return;
    }

    if (SelectedSourceFeedType == sourceFeedType) return;
    SelectedSourceFeedType = sourceFeedType;

    if (IsSourceScopeSelected)
    {
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  private Task<NewsFeedPage> LoadNewsFeedPageAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      string? after,
      CancellationToken cancellationToken) =>
      scope.IsSourcesScope()
          ? newsFeedService.GetNewsFeedPageAsync(
              scope, sourceFeedType, after, limit: 25, cancellationToken: cancellationToken)
          : newsFeedService.GetNewsFeedPageAsync(scope, after, cancellationToken: cancellationToken);
}
