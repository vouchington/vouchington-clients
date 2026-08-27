using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Profiles;

public enum NativeUserProfileSection
{
  Overview,
  Posts,
  Topics,
  Friends,
  Sources,
  Communities,
}

public enum NativeUserProfileCollectionKind
{
  None,
  PostsAll,
  Reviews,
  Discussions,
  Comments,
  TopicsFollowing,
  UsersFollowing,
  UsersFollowers,
  SourcesAll,
  SourcesNews,
  SourcesPodcasts,
  SourcesVideos,
  CommunitiesMember,
}

public sealed record NativeUserProfileScope(
    NativeUserProfileSection Section,
    NativeUserProfileCollectionKind Collection)
{
  public static NativeUserProfileScope Overview { get; } =
      new(NativeUserProfileSection.Overview, NativeUserProfileCollectionKind.None);

  public bool IsOverview => Collection == NativeUserProfileCollectionKind.None;

  public string? FeedType => Collection switch
  {
    NativeUserProfileCollectionKind.SourcesNews => "article",
    NativeUserProfileCollectionKind.SourcesPodcasts => "podcast",
    NativeUserProfileCollectionKind.SourcesVideos => "video",
    _ => null,
  };

  public static bool TryParse(NativeRouteMatch match, out NativeUserProfileScope scope)
  {
    ArgumentNullException.ThrowIfNull(match);
    var subpath = NativeUserProfileRoutePaths.Subpath(match.Path);
    NativeUserProfileScope? parsed = subpath?.ToUpperInvariant() switch
    {
      null => Overview,
      "/POSTS" => Posts(NativeUserProfileCollectionKind.PostsAll),
      "/REVIEWS" => Posts(NativeUserProfileCollectionKind.Reviews),
      "/DISCUSSIONS" => Posts(NativeUserProfileCollectionKind.Discussions),
      "/COMMENTS" => Posts(NativeUserProfileCollectionKind.Comments),
      "/TOPICS/FOLLOWING" => new(NativeUserProfileSection.Topics, NativeUserProfileCollectionKind.TopicsFollowing),
      "/USERS/FOLLOWING" => new(NativeUserProfileSection.Friends, NativeUserProfileCollectionKind.UsersFollowing),
      "/USERS/FOLLOWERS" => new(NativeUserProfileSection.Friends, NativeUserProfileCollectionKind.UsersFollowers),
      "/COMMUNITIES/MEMBER" => new(NativeUserProfileSection.Communities, NativeUserProfileCollectionKind.CommunitiesMember),
      "/RSS-FEEDS/FOLLOWING" => Sources(match.QueryValue("feed_type")),
      _ => null,
    };
    scope = parsed ?? Overview;
    return parsed is not null;
  }

  private static NativeUserProfileScope Posts(NativeUserProfileCollectionKind collection) =>
      new(NativeUserProfileSection.Posts, collection);

  private static NativeUserProfileScope? Sources(string? feedType) => feedType?.ToUpperInvariant() switch
  {
    null or "" => new(NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesAll),
    "ARTICLE" => new(NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesNews),
    "PODCAST" => new(NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesPodcasts),
    "VIDEO" => new(NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesVideos),
    _ => null,
  };
}
