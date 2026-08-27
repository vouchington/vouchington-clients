namespace Voucha.Client.Core.NewsFeeds;

public sealed class SampleNewsFeedService : INewsFeedService
{
  private static readonly IReadOnlyList<NewsFeedItem> YourFeedItems =
  [
      new(
            "your-wire-brief",
            "Semiconductor policy shifts as chip funding rules tighten",
            "Policy Wire",
            "A concise roundup of regulatory updates, funding windows, and supply-chain signals followed by your network.",
            new Uri("https://example.com/news/semiconductor-policy"),
            new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero)),
        new(
            "your-energy-grid",
            "Grid storage projects move from pilots to procurement",
            "Energy Dispatch",
            "Operators are standardizing battery and demand-response requirements after a year of reliability tests.",
            new Uri("https://example.com/news/grid-storage"),
            new DateTimeOffset(2026, 6, 28, 7, 45, 0, TimeSpan.Zero)),
    ];

  private static readonly IReadOnlyList<NewsFeedItem> AllNewsItems =
  [
      .. YourFeedItems,
        new(
            "all-health-data",
            "Hospitals expand patient-data interoperability pilots",
            "Health Systems Today",
            "Regional exchanges are testing cleaner handoffs between clinical records, claims data, and patient apps.",
            new Uri("https://example.com/news/health-data"),
            new DateTimeOffset(2026, 6, 27, 18, 30, 0, TimeSpan.Zero)),
        new(
            "all-climate-reporting",
            "Climate disclosure teams converge on audit-ready workflows",
            "Markets Desk",
            "Finance teams are treating emissions data like quarterly reporting, with stronger controls and review trails.",
            new Uri("https://example.com/news/climate-reporting"),
            new DateTimeOffset(2026, 6, 27, 16, 15, 0, TimeSpan.Zero)),
    ];

  private static readonly IReadOnlyList<NewsFeedItem> PodcastItems =
  [
      new(
            "podcast-ep-1",
            "Product design review with the platform team",
            "Voucha Podcast",
            "A recorded discussion about release planning, metrics, and the UX changes shipping this week.",
            new Uri("https://example.com/podcasts/product-design-review"),
            new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero),
            NewsFeedItemKind.Media,
            MediaUrl: new Uri("https://cdn.example.com/podcasts/design-review.mp3"),
            ThumbnailUrl: new Uri("https://cdn.example.com/podcasts/design-review.jpg"),
            ProtocolMediaType: "audio",
            DurationSeconds: 1800),
  ];

  private static readonly IReadOnlyList<NewsFeedItem> VideoItems =
  [
      new(
            "video-ep-1",
            "Release demo: native playback on .NET MAUI",
            "Voucha Video",
            "A walkthrough of the media feed and playback surfaces from the latest app release.",
            new Uri("https://example.com/videos/native-playback"),
            new DateTimeOffset(2026, 6, 28, 13, 30, 0, TimeSpan.Zero),
            NewsFeedItemKind.Media,
            MediaUrl: new Uri("https://cdn.example.com/videos/native-playback.mp4"),
            ThumbnailUrl: new Uri("https://cdn.example.com/videos/native-playback.jpg"),
            ProtocolMediaType: "video",
            VideoPlatform: "peertube",
            VideoId: "native-playback",
            DurationSeconds: 1460),
  ];

  public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      CancellationToken cancellationToken = default)
  {
    return GetNewsFeedItemsAsync(scope, NewsFeedSourceType.Article, cancellationToken);
  }

  public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    IReadOnlyList<NewsFeedItem> items = scope switch
    {
      NewsFeedScope.YourFeed => YourFeedItems,
      NewsFeedScope.AllNews => AllNewsItems,
      NewsFeedScope.YourSources => [],
      NewsFeedScope.AllSources => [],
      NewsFeedScope.YourPodcasts => PodcastItems,
      NewsFeedScope.AllPodcasts => PodcastItems,
      NewsFeedScope.YourPodcastSources => [],
      NewsFeedScope.AllPodcastSources => [],
      NewsFeedScope.YourVideos => VideoItems,
      NewsFeedScope.AllVideos => VideoItems,
      NewsFeedScope.YourVideoSources => [],
      NewsFeedScope.AllVideoSources => [],
      _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown news feed scope."),
    };

    return Task.FromResult(items);
  }

  public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task VoteRssFeedItemAsync(string itemId, Api.ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task ClearRssFeedItemVoteAsync(string itemId, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task VoteTopicAsync(string topicId, Api.ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
}
