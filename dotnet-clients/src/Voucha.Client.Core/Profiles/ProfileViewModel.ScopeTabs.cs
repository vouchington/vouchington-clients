using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private IReadOnlyList<ProfileScopeTabRow> BuildContextualTabs(UserMetricsCount counts) =>
      ProfileScope.Section switch
      {
        NativeUserProfileSection.Posts =>
        [
          ContextRow(NativeUserProfileCollectionKind.PostsAll, UiMessageKey.NativeDotnetProfileAll, counts.Reviews + counts.Discussions + counts.Comments),
          ContextRow(NativeUserProfileCollectionKind.Reviews, UiMessageKey.NativeDotnetProfileReviews, counts.Reviews),
          ContextRow(NativeUserProfileCollectionKind.Discussions, UiMessageKey.NativeDotnetProfileDiscussions, counts.Discussions),
          ContextRow(NativeUserProfileCollectionKind.Comments, UiMessageKey.NativeDotnetProfileComments, counts.Comments),
        ],
        NativeUserProfileSection.Friends =>
        [
          ContextRow(NativeUserProfileCollectionKind.UsersFollowing, UiMessageKey.NativeDotnetProfileFollowing, counts.UsersFollowing),
          ContextRow(NativeUserProfileCollectionKind.UsersFollowers, UiMessageKey.NativeDotnetProfileFollowers, counts.UsersFollowers),
        ],
        NativeUserProfileSection.Sources =>
        [
          ContextRow(NativeUserProfileCollectionKind.SourcesAll, UiMessageKey.NativeDotnetProfileAll, counts.RssFeedsFollowing, alwaysVisible: true),
          ContextRow(NativeUserProfileCollectionKind.SourcesNews, UiMessageKey.NativeDotnetProfileNews, 0, alwaysVisible: true),
          ContextRow(NativeUserProfileCollectionKind.SourcesPodcasts, UiMessageKey.NativeDotnetProfilePodcasts, 0, alwaysVisible: true),
          ContextRow(NativeUserProfileCollectionKind.SourcesVideos, UiMessageKey.NativeDotnetProfileVideos, 0, alwaysVisible: true),
        ],
        _ => [],
      };

  private ProfileScopeTabRow ContextRow(
      NativeUserProfileCollectionKind collection,
      UiMessageKey labelKey,
      int count,
      bool alwaysVisible = false) =>
      new(
          ProfileScope.Section,
          collection,
          UiText.Localized(labelKey),
          count,
          ProfileScope.Collection == collection,
          alwaysVisible || count > 0 || ProfileScope.Collection == collection,
          localization);

  private static NativeUserProfileSection SectionFor(NativeUserProfileCollectionKind collection) => collection switch
  {
    NativeUserProfileCollectionKind.None => NativeUserProfileSection.Overview,
    NativeUserProfileCollectionKind.PostsAll or NativeUserProfileCollectionKind.Reviews or
        NativeUserProfileCollectionKind.Discussions or NativeUserProfileCollectionKind.Comments => NativeUserProfileSection.Posts,
    NativeUserProfileCollectionKind.TopicsFollowing => NativeUserProfileSection.Topics,
    NativeUserProfileCollectionKind.UsersFollowing or NativeUserProfileCollectionKind.UsersFollowers => NativeUserProfileSection.Friends,
    NativeUserProfileCollectionKind.SourcesAll or NativeUserProfileCollectionKind.SourcesNews or
        NativeUserProfileCollectionKind.SourcesPodcasts or NativeUserProfileCollectionKind.SourcesVideos => NativeUserProfileSection.Sources,
    NativeUserProfileCollectionKind.CommunitiesMember => NativeUserProfileSection.Communities,
    _ => NativeUserProfileSection.Overview,
  };

  private static ProfileHistoryTab HistoryTabFor(NativeUserProfileCollectionKind collection) => collection switch
  {
    NativeUserProfileCollectionKind.Reviews => ProfileHistoryTab.Reviews,
    NativeUserProfileCollectionKind.Discussions => ProfileHistoryTab.Discussions,
    NativeUserProfileCollectionKind.Comments => ProfileHistoryTab.Comments,
    _ => ProfileHistoryTab.All,
  };
}
