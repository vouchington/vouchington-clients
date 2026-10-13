using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  public static TheoryData<NativeUserProfileCollectionKind, string> CollectionScopes => new()
  {
    { NativeUserProfileCollectionKind.TopicsFollowing, "topics" },
    { NativeUserProfileCollectionKind.UsersFollowing, "following" },
    { NativeUserProfileCollectionKind.UsersFollowers, "followers" },
    { NativeUserProfileCollectionKind.SourcesAll, "sources:" },
    { NativeUserProfileCollectionKind.SourcesNews, "sources:article" },
    { NativeUserProfileCollectionKind.SourcesPodcasts, "sources:podcast" },
    { NativeUserProfileCollectionKind.SourcesVideos, "sources:video" },
    { NativeUserProfileCollectionKind.CommunitiesMember, "communities" },
  };

  [Theory]
  [InlineData(NativeUserProfileCollectionKind.TopicsFollowing)]
  [InlineData(NativeUserProfileCollectionKind.SourcesAll)]
  [InlineData(NativeUserProfileCollectionKind.CommunitiesMember)]
  public async Task ProfileCollectionRowsRetainServerProvenanceAndProjectCurrentStaffTier(
      NativeUserProfileCollectionKind collection)
  {
    var service = new RecordingProfileCollectionsService { IncludeProvenance = true };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);
    await viewModel.LoadPublicScopeAsync("bob",
        new NativeUserProfileScope(SectionForTest(collection), collection), TestContext.Current.CancellationToken);

    Assert.True(CollectionHasPublicProvenance(viewModel, collection));
    Assert.False(CollectionHasStaffChannel(viewModel, collection));
    viewModel.SetCurrentViewer("staff-1", "staff", isStaffViewer: true);
    Assert.True(CollectionHasStaffChannel(viewModel, collection));
    viewModel.SetCurrentViewer(null, null);
    Assert.False(CollectionHasStaffChannel(viewModel, collection));
  }

  private static bool CollectionHasPublicProvenance(ProfileViewModel viewModel, NativeUserProfileCollectionKind collection) =>
      collection switch
      {
        NativeUserProfileCollectionKind.TopicsFollowing => Assert.Single(viewModel.TopicItems).HasProvenance,
        NativeUserProfileCollectionKind.SourcesAll => Assert.Single(viewModel.SourceItems).HasProvenance,
        NativeUserProfileCollectionKind.CommunitiesMember => Assert.Single(viewModel.CommunityItems).HasProvenance,
        _ => throw new ArgumentOutOfRangeException(nameof(collection)),
      };

  private static bool CollectionHasStaffChannel(ProfileViewModel viewModel, NativeUserProfileCollectionKind collection) =>
      collection switch
      {
        NativeUserProfileCollectionKind.TopicsFollowing => Assert.Single(viewModel.TopicItems).HasStaffChannel,
        NativeUserProfileCollectionKind.SourcesAll => Assert.Single(viewModel.SourceItems).HasStaffChannel,
        NativeUserProfileCollectionKind.CommunitiesMember => Assert.Single(viewModel.CommunityItems).HasStaffChannel,
        _ => throw new ArgumentOutOfRangeException(nameof(collection)),
      };

  [Theory]
  [MemberData(nameof(CollectionScopes))]
  public async Task EveryCollectionScopeSelectsExactServiceAndTypedRows(
      NativeUserProfileCollectionKind collection,
      string expectedCall)
  {
    var service = new RecordingProfileCollectionsService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(SectionForTest(collection), collection),
        TestContext.Current.CancellationToken);

    Assert.Equal(expectedCall, Assert.Single(service.Calls));
    Assert.Contains(viewModel.PrimaryTabs, row => row.Section == SectionForTest(collection) && row.IsSelected);
    if (SectionForTest(collection) is NativeUserProfileSection.Friends or NativeUserProfileSection.Sources)
    {
      Assert.Contains(viewModel.ContextualTabs, row => row.Collection == collection && row.IsSelected && row.IsVisible);
    }
    Assert.True(viewModel.TopicItems.Count + viewModel.FriendItems.Count +
        viewModel.SourceItems.Count + viewModel.CommunityItems.Count > 0);
  }

  [Fact]
  public async Task CollectionFailurePreservesHeaderAndSelectedTabCanRetry()
  {
    var service = new RecordingProfileCollectionsService { Failure = new InvalidOperationException("Collection failed") };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);
    var scope = new NativeUserProfileScope(
        NativeUserProfileSection.Topics,
        NativeUserProfileCollectionKind.TopicsFollowing);

    await viewModel.LoadPublicScopeAsync("bob", scope, TestContext.Current.CancellationToken);

    Assert.Equal("bob", viewModel.User?.Username);
    Assert.Equal("Collection failed", viewModel.CollectionErrorMessage);
    service.Failure = null;

    await viewModel.SelectScopeAsync(
        NativeUserProfileCollectionKind.TopicsFollowing,
        TestContext.Current.CancellationToken);

    Assert.Equal("bob", viewModel.User?.Username);
    Assert.Null(viewModel.CollectionErrorMessage);
    Assert.Single(viewModel.TopicItems);
  }

  [Fact]
  public async Task FailedFirstPageReloadClearsPreviousCursorAndDisablesLoadMore()
  {
    var service = new RecordingProfileCollectionsService { HasNextPage = true };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);
    var scope = new NativeUserProfileScope(
        NativeUserProfileSection.Topics,
        NativeUserProfileCollectionKind.TopicsFollowing);

    await viewModel.LoadPublicScopeAsync("bob", scope, TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanLoadMore);

    service.Failure = new InvalidOperationException("Reload failed");
    await viewModel.SelectScopeAsync(
        NativeUserProfileCollectionKind.TopicsFollowing,
        TestContext.Current.CancellationToken);

    Assert.Equal("Reload failed", viewModel.CollectionErrorMessage);
    Assert.False(viewModel.CanLoadMore);
    Assert.Single(viewModel.TopicItems);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, null], service.Afters);
  }

  [Theory]
  [InlineData(NativeUserProfileCollectionKind.TopicsFollowing)]
  [InlineData(NativeUserProfileCollectionKind.UsersFollowing)]
  [InlineData(NativeUserProfileCollectionKind.SourcesNews)]
  [InlineData(NativeUserProfileCollectionKind.CommunitiesMember)]
  public async Task EveryCollectionEndpointFamilyContinuesWithStableCursor(
      NativeUserProfileCollectionKind collection)
  {
    var service = new RecordingProfileCollectionsService { HasNextPage = true };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(SectionForTest(collection), collection),
        TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "cursor-1"], service.Afters);
    Assert.False(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task SparseViewerPostCountsOverlayCompletePublicProfileCounts()
  {
    var metrics = new UserMetrics(
        "user",
        "user-2",
        new UserMetricsCount(
            Reviews: 4,
            Discussions: 3,
            Comments: 2,
            UsersFollowing: 5,
            UsersFollowers: 6,
            TopicsFollowing: 7,
            RssFeedsFollowing: 8,
            CommunitiesMember: 9),
        new UserMetricsViewerCount(Reviews: 0));
    var viewModel = NewViewModel(
        new User("user-2", "bob", "Hello"),
        metrics: metrics);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(
            NativeUserProfileSection.Posts,
            NativeUserProfileCollectionKind.Reviews),
        TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.HistoryTabs, row =>
        row.Tab == ProfileHistoryTab.All && row.Count == 5);
    Assert.Contains(viewModel.HistoryTabs, row =>
        row.Tab == ProfileHistoryTab.Reviews && row.Count == 0);
    Assert.Contains(viewModel.HistoryTabs, row =>
        row.Tab == ProfileHistoryTab.Discussions && row.Count == 3);
    Assert.Contains(viewModel.HistoryTabs, row =>
        row.Tab == ProfileHistoryTab.Comments && row.Count == 2);
    Assert.Contains(viewModel.PrimaryTabs, row =>
        row.Section == NativeUserProfileSection.Topics && row.Count == 7);
    Assert.Contains(viewModel.PrimaryTabs, row =>
        row.Section == NativeUserProfileSection.Friends && row.Count == 11);
    Assert.Contains(viewModel.PrimaryTabs, row =>
        row.Section == NativeUserProfileSection.Sources && row.Count == 8);
    Assert.Contains(viewModel.PrimaryTabs, row =>
        row.Section == NativeUserProfileSection.Communities && row.Count == 9);
    Assert.Contains(viewModel.ContextualTabs, row =>
        row.Collection == NativeUserProfileCollectionKind.Reviews &&
        row.Count == 0 &&
        row.IsSelected &&
        row.IsVisible);
  }

  [Fact]
  public async Task FriendsPrimaryTabTargetsFollowersWhenFollowingIsHidden()
  {
    var metrics = new UserMetrics(
        "user_metrics",
        "user-2",
        new UserMetricsCount(UsersFollowing: 0, UsersFollowers: 2));
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), metrics: metrics);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        NativeUserProfileScope.Overview,
        TestContext.Current.CancellationToken);

    var friends = Assert.Single(
        viewModel.PrimaryTabs,
        row => row.Section == NativeUserProfileSection.Friends);
    Assert.Equal(NativeUserProfileCollectionKind.UsersFollowers, friends.Collection);
  }

  [Fact]
  public async Task UnknownCollectionFallsBackToOverview()
  {
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));

    await viewModel.SelectScopeAsync(
        (NativeUserProfileCollectionKind)int.MaxValue,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeUserProfileScope.Overview, viewModel.ProfileScope);
  }

  [Fact]
  public void UnexpectedProfileActionErrorIsVisible()
  {
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));

    viewModel.ReportUnexpectedProfileActionError(new InvalidOperationException("Action failed"));

    Assert.Equal("Action failed", viewModel.CollectionErrorMessage);
  }

  [Fact]
  public async Task RestrictedCountsHideNonSelectedTabsButKeepSelectedDeepLinkVisible()
  {
    var metrics = new UserMetrics(
        "user_metrics",
        "user-2",
        new UserMetricsCount(Reviews: 4, Discussions: 3, Comments: 2));
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), metrics: metrics);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(
            NativeUserProfileSection.Friends,
            NativeUserProfileCollectionKind.UsersFollowing),
        TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.PrimaryTabs, row =>
        row.Section == NativeUserProfileSection.Friends && row.IsSelected && row.IsVisible);
    Assert.DoesNotContain(viewModel.PrimaryTabs, row =>
        (row.Section is NativeUserProfileSection.Topics or
            NativeUserProfileSection.Sources or
            NativeUserProfileSection.Communities) &&
        row.IsVisible);
    Assert.Contains(viewModel.ContextualTabs, row =>
        row.Collection == NativeUserProfileCollectionKind.UsersFollowing &&
        row.IsSelected &&
        row.IsVisible);
    Assert.Contains(viewModel.ContextualTabs, row =>
        row.Collection == NativeUserProfileCollectionKind.UsersFollowers && !row.IsVisible);
  }

  [Fact]
  public async Task RefreshPublicCollectionReloadsSelectedScopeAndReplacesCursor()
  {
    var posts = new RecordingPostsService();
    var service = new RecordingProfileCollectionsService
    {
      TopicsOverride = (call, after) => call switch
      {
        1 => TopicPage("topic-initial", "cursor-initial", true),
        2 => TopicPage("topic-refreshed", "cursor-refreshed", true),
        3 when after == "cursor-refreshed" => TopicPage("topic-next", null, false),
        _ => throw new InvalidOperationException($"Unexpected topics request {call} after {after}"),
      },
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);
    viewModel.ConfigureProfileCollectionsService(service);
    var scope = new NativeUserProfileScope(
        NativeUserProfileSection.Topics,
        NativeUserProfileCollectionKind.TopicsFollowing);

    await viewModel.LoadPublicScopeAsync("bob", scope, TestContext.Current.CancellationToken);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["topic-refreshed"], viewModel.TopicItems.Select(row => row.Id));
    Assert.Equal(0, posts.CallCount);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, null, "cursor-refreshed"], service.Afters);
    Assert.Equal(["topic-refreshed", "topic-next"], viewModel.TopicItems.Select(row => row.Id));
    Assert.False(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task FollowingScopeKeepsPlacementFromPublicUserFixture()
  {
    var response = JsonSerializer.Deserialize<UserFollowingResponse>(
        ApiFixtureLoader.LoadResponse("swift.users.following.default"), VouchaApiJson.Options)!;
    var service = new RecordingProfileCollectionsService { FriendOverride = Assert.Single(response.Results) };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(NativeUserProfileSection.Friends, NativeUserProfileCollectionKind.UsersFollowing),
        TestContext.Current.CancellationToken);

    Assert.Equal("00000000-0000-7000-8000-000000000301", Assert.Single(viewModel.FriendItems).ProfileImagePlacement?.PlacementId);
  }

  [Fact]
  public async Task FollowingTopicsScopeKeepsLogoPlacementFromPublicProfileFixture()
  {
    var page = JsonSerializer.Deserialize<TopicFixturePage>(
        ApiFixtureLoader.LoadResponse("native.users.profile.topics-following.first-page"), VouchaApiJson.Options)!;
    var service = new RecordingProfileCollectionsService
    {
      TopicsOverride = (_, _) => new ProfileCollectionPage<Topic>(page.Results, page.PageInfo),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureProfileCollectionsService(service);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(NativeUserProfileSection.Topics, NativeUserProfileCollectionKind.TopicsFollowing),
        TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.TopicItems);
    Assert.Equal("00000000-0000-7000-8000-000000000306", row.LogoImagePlacement?.PlacementId);
    Assert.Equal(3, row.LogoImagePlacement?.PlacementRevision);
    Assert.Equal("00000000-0000-7000-8000-000000000205", row.LogoImagePlacement?.ImageId);
  }

  private sealed record TopicFixturePage(
      [property: JsonPropertyName("results")] IReadOnlyList<Topic> Results,
      [property: JsonPropertyName("page_info")] PageInfo PageInfo);

  private static ProfileCollectionPage<Topic> TopicPage(string id, string? cursor, bool hasNextPage) =>
      new([new Topic(id, id, id, "topic")], new PageInfo(cursor, hasNextPage, null));

  private static NativeUserProfileSection SectionForTest(NativeUserProfileCollectionKind collection) => collection switch
  {
    NativeUserProfileCollectionKind.TopicsFollowing => NativeUserProfileSection.Topics,
    NativeUserProfileCollectionKind.UsersFollowing or NativeUserProfileCollectionKind.UsersFollowers => NativeUserProfileSection.Friends,
    NativeUserProfileCollectionKind.SourcesAll or NativeUserProfileCollectionKind.SourcesNews or
        NativeUserProfileCollectionKind.SourcesPodcasts or NativeUserProfileCollectionKind.SourcesVideos => NativeUserProfileSection.Sources,
    NativeUserProfileCollectionKind.CommunitiesMember => NativeUserProfileSection.Communities,
    _ => throw new ArgumentOutOfRangeException(nameof(collection)),
  };

  private sealed class RecordingProfileCollectionsService : IProfileCollectionsService
  {
    private int topicsCallCount;

    public List<string> Calls { get; } = [];
    public List<string?> Afters { get; } = [];
    public Exception? Failure { get; set; }
    public bool HasNextPage { get; set; }
    public bool IncludeProvenance { get; init; }
    public User? FriendOverride { get; init; }
    public Func<int, string?, ProfileCollectionPage<Topic>>? TopicsOverride { get; init; }

    public Task<ProfileCollectionPage<Topic>> FetchTopicsAsync(
        string userId,
        string? after,
        CancellationToken cancellationToken = default)
    {
      if (TopicsOverride is null)
      {
        return Result("topics", after, new Topic("topic-1", "Topic", "topic", "topic",
            Provenance: IncludeProvenance ? new PublicContentProvenance("mcp", null) : null,
            StaffProvenance: IncludeProvenance ? new StaffContentProvenance("mcp") : null));
      }
      Calls.Add("topics");
      Afters.Add(after);
      return Task.FromResult(TopicsOverride(++topicsCallCount, after));
    }

    public Task<ProfileCollectionPage<User>> FetchFollowingAsync(string userId, string? after, CancellationToken cancellationToken = default) =>
        Result("following", after, FriendOverride ?? new User("friend-1", "friend"));

    public Task<ProfileCollectionPage<User>> FetchFollowersAsync(string userId, string? after, CancellationToken cancellationToken = default) =>
        Result("followers", after, new User("friend-1", "friend"));

    public Task<ProfileCollectionPage<RssFeedSource>> FetchSourcesAsync(
        string userId, string? feedType, string? after, CancellationToken cancellationToken = default) =>
        Result($"sources:{feedType}", after, new RssFeedSource("source-1", "Source", null, null, null, null,
            Provenance: IncludeProvenance ? new PublicContentProvenance("mcp", null) : null,
            StaffProvenance: IncludeProvenance ? new StaffContentProvenance("mcp") : null));

    public Task<ProfileCollectionPage<Community>> FetchCommunitiesAsync(string userId, string? after, CancellationToken cancellationToken = default) =>
        Result("communities", after, new Community(
            "community-1", "Community", "community", null, "public", "public", null,
            null, null, false, false, "user-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Provenance: IncludeProvenance ? new PublicContentProvenance("mcp", null) : null,
            StaffProvenance: IncludeProvenance ? new StaffContentProvenance("mcp") : null));

    private Task<ProfileCollectionPage<T>> Result<T>(string call, string? after, T item)
    {
      Calls.Add(call);
      Afters.Add(after);
      if (Failure is not null) return Task.FromException<ProfileCollectionPage<T>>(Failure);
      var firstPage = HasNextPage && after is null;
      return Task.FromResult(new ProfileCollectionPage<T>(
          [item], new PageInfo(firstPage ? "cursor-1" : null, firstPage, null)));
    }
  }
}
