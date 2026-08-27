using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private async Task FetchAndApplyCollectionAsync(
      string userId,
      NativeUserProfileScope scope,
      string? after,
      bool append,
      int version,
      CancellationToken cancellationToken)
  {
    switch (scope.Collection)
    {
      case NativeUserProfileCollectionKind.TopicsFollowing:
        var topics = await profileCollectionsService!.FetchTopicsAsync(userId, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrent(version, scope)) return;
        TopicItems = Merge(TopicItems, topics.Results.Select(ToTopicRow), append, row => row.Id);
        collectionPageInfo = topics.PageInfo;
        break;
      case NativeUserProfileCollectionKind.UsersFollowing:
      case NativeUserProfileCollectionKind.UsersFollowers:
        var users = scope.Collection == NativeUserProfileCollectionKind.UsersFollowing
            ? await profileCollectionsService!.FetchFollowingAsync(userId, after, cancellationToken).ConfigureAwait(true)
            : await profileCollectionsService!.FetchFollowersAsync(userId, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrent(version, scope)) return;
        FriendItems = Merge(
            FriendItems,
            users.Results.Select(user => ToFriendRow(user, scope.Collection == NativeUserProfileCollectionKind.UsersFollowing)),
            append,
            row => row.Id);
        collectionPageInfo = users.PageInfo;
        break;
      case NativeUserProfileCollectionKind.SourcesAll:
      case NativeUserProfileCollectionKind.SourcesNews:
      case NativeUserProfileCollectionKind.SourcesPodcasts:
      case NativeUserProfileCollectionKind.SourcesVideos:
        var sources = await profileCollectionsService!.FetchSourcesAsync(userId, scope.FeedType, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrent(version, scope)) return;
        SourceItems = Merge(SourceItems, sources.Results, append, row => row.Id);
        collectionPageInfo = sources.PageInfo;
        break;
      case NativeUserProfileCollectionKind.CommunitiesMember:
        var communities = await profileCollectionsService!.FetchCommunitiesAsync(userId, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrent(version, scope)) return;
        CommunityItems = Merge(CommunityItems, communities.Results.Select(ToCommunityRow), append, row => row.Id);
        collectionPageInfo = communities.PageInfo;
        break;
      default:
        throw new InvalidOperationException("The selected profile scope is not a collection.");
    }
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private bool IsCurrent(int version, NativeUserProfileScope scope) =>
      version == collectionLoadVersion && scope == ProfileScope;

  private static T[] Merge<T>(
      IReadOnlyList<T> current,
      IEnumerable<T> next,
      bool append,
      Func<T, string> id)
  {
    var source = append ? current.Concat(next) : next;
    return source.DistinctBy(id, StringComparer.Ordinal).ToArray();
  }

  private FriendRow ToFriendRow(User user, bool following) => new(
      user.Id,
      user.VerifiedDisplayName ?? user.Name,
      null,
      user.Username,
      user.ProfileImageId,
      following,
      string.Equals(user.Id, currentViewerUserId, StringComparison.Ordinal));

  private TopicRow ToTopicRow(Topic topic) => new(
      topic.Id,
      topic.Name,
      topic.Slug,
      topic.TopicType,
      UiTaxonomy.TopicType(topic.TopicType),
      topic.Markdown,
      Localization: localization);

  private static CommunityBrowseRow ToCommunityRow(Community community) => new(
      community.Id,
      community.Name,
      community.Slug,
      0,
      0,
      community.ArchivedAt is not null);
}
