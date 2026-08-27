using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private IProfileCollectionsService? profileCollectionsService;
  private NativeUserProfileScope profileScope = NativeUserProfileScope.Overview;
  private IReadOnlyList<ProfileScopeTabRow> primaryTabs = [];
  private IReadOnlyList<ProfileScopeTabRow> contextualTabs = [];
  private IReadOnlyList<FriendRow> friendItems = [];
  private IReadOnlyList<TopicRow> topicItems = [];
  private IReadOnlyList<RssFeedSource> sourceItems = [];
  private IReadOnlyList<CommunityBrowseRow> communityItems = [];
  private PageInfo? collectionPageInfo;
  private string? collectionErrorMessage;
  private bool isLoadingMore;
  private int loadMoreGuard;
  private int collectionLoadVersion;

  public NativeUserProfileScope ProfileScope
  {
    get => profileScope;
    private set
    {
      if (SetProperty(ref profileScope, value))
      {
        OnPropertyChanged(nameof(IsOverviewScope));
        OnPropertyChanged(nameof(IsPostsScope));
        OnPropertyChanged(nameof(IsFriendsScope));
        OnPropertyChanged(nameof(IsTopicsScope));
        OnPropertyChanged(nameof(IsSourcesScope));
        OnPropertyChanged(nameof(IsCommunitiesScope));
        OnPropertyChanged(nameof(CanShowAdminLandingPages));
      }
    }
  }

  public bool IsOverviewScope => ProfileScope.IsOverview;
  public bool IsPostsScope => ProfileScope.Section == NativeUserProfileSection.Posts || IsOverviewScope;
  public bool IsFriendsScope => ProfileScope.Section == NativeUserProfileSection.Friends;
  public bool IsTopicsScope => ProfileScope.Section == NativeUserProfileSection.Topics;
  public bool IsSourcesScope => ProfileScope.Section == NativeUserProfileSection.Sources;
  public bool IsCommunitiesScope => ProfileScope.Section == NativeUserProfileSection.Communities;
  public IReadOnlyList<ProfileScopeTabRow> PrimaryTabs { get => primaryTabs; private set => SetProperty(ref primaryTabs, value); }
  public IReadOnlyList<ProfileScopeTabRow> ContextualTabs { get => contextualTabs; private set => SetProperty(ref contextualTabs, value); }
  public IReadOnlyList<FriendRow> FriendItems { get => friendItems; private set => SetProperty(ref friendItems, value); }
  public IReadOnlyList<TopicRow> TopicItems { get => topicItems; private set => SetProperty(ref topicItems, value); }
  public IReadOnlyList<RssFeedSource> SourceItems { get => sourceItems; private set => SetProperty(ref sourceItems, value); }
  public IReadOnlyList<CommunityBrowseRow> CommunityItems { get => communityItems; private set => SetProperty(ref communityItems, value); }
  public string? CollectionErrorMessage { get => collectionErrorMessage; private set => SetProperty(ref collectionErrorMessage, value); }
  public bool IsLoadingMore { get => isLoadingMore; private set => SetProperty(ref isLoadingMore, value); }
  public bool CanLoadMore => (IsPostsScope ? historyPageInfo : collectionPageInfo)?.HasNextPage == true && !IsLoadingMore;

  public void ConfigureProfileCollectionsService(IProfileCollectionsService service) =>
      profileCollectionsService = service ?? throw new ArgumentNullException(nameof(service));

  public void ReportUnexpectedProfileActionError(Exception error)
  {
    ArgumentNullException.ThrowIfNull(error);
    CollectionErrorMessage = error.Message;
  }

  private void ApplyScope(NativeUserProfileScope scope)
  {
    ProfileScope = scope;
    BuildScopeTabs();
    FriendItems = [];
    TopicItems = [];
    SourceItems = [];
    CommunityItems = [];
    collectionPageInfo = null;
    CollectionErrorMessage = null;
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private void BuildScopeTabs()
  {
    var counts = ViewerVisibleCounts(userMetrics);
    var friendsCollection = counts.UsersFollowing > 0
        ? NativeUserProfileCollectionKind.UsersFollowing
        : counts.UsersFollowers > 0
            ? NativeUserProfileCollectionKind.UsersFollowers
            : ProfileScope.Section == NativeUserProfileSection.Friends
                ? ProfileScope.Collection
                : NativeUserProfileCollectionKind.UsersFollowing;
    PrimaryTabs =
    [
      ScopeRow(NativeUserProfileSection.Overview, NativeUserProfileCollectionKind.None, UiMessageKey.NativeDotnetProfileOverview, 1),
      ScopeRow(NativeUserProfileSection.Posts, NativeUserProfileCollectionKind.PostsAll, UiMessageKey.NativeDotnetProfilePosts, counts.Reviews + counts.Discussions + counts.Comments),
      ScopeRow(NativeUserProfileSection.Topics, NativeUserProfileCollectionKind.TopicsFollowing, UiMessageKey.NativeDotnetProfileTopics, counts.TopicsFollowing),
      ScopeRow(NativeUserProfileSection.Friends, friendsCollection, UiMessageKey.NativeDotnetProfileFriends, counts.UsersFollowing + counts.UsersFollowers),
      ScopeRow(NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesAll, UiMessageKey.NativeDotnetProfileSources, counts.RssFeedsFollowing),
      ScopeRow(NativeUserProfileSection.Communities, NativeUserProfileCollectionKind.CommunitiesMember, UiMessageKey.NativeDotnetProfileCommunities, counts.CommunitiesMember),
    ];
    ContextualTabs = BuildContextualTabs(counts);
  }

  private static UserMetricsCount ViewerVisibleCounts(UserMetrics? metrics)
  {
    var publicCounts = metrics?.Count ?? new();
    var viewerCounts = metrics?.ViewerCount;
    return publicCounts with
    {
      Reviews = viewerCounts?.Reviews ?? publicCounts.Reviews,
      Discussions = viewerCounts?.Discussions ?? publicCounts.Discussions,
      Comments = viewerCounts?.Comments ?? publicCounts.Comments,
    };
  }

  private ProfileScopeTabRow ScopeRow(NativeUserProfileSection section, NativeUserProfileCollectionKind collection, UiMessageKey labelKey, int count) =>
      new(section, collection, UiText.Localized(labelKey), count, ProfileScope.Section == section, count > 0 || ProfileScope.Section == section || section == NativeUserProfileSection.Overview, localization);
}
