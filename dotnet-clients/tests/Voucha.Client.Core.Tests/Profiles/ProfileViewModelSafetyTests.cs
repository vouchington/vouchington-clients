using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task LoadPublicAsyncLoadsSafetyStateForSignedInNonSelfProfile()
  {
    var safety = new RecordingProfileSafetyService
    {
      Bookmarks = new BookmarkPredicates(Mute: true, Block: true),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanActOnUser);
    Assert.True(viewModel.CanViewUserTags);
    Assert.True(viewModel.IsMutedUser);
    Assert.True(viewModel.IsBlockedUser);
    Assert.False(viewModel.CanFollowUser);
    Assert.False(viewModel.CanUnfollowUser);
    Assert.Equal("Unmute", viewModel.MuteUserButtonText);
    Assert.Equal("Unblock", viewModel.BlockUserButtonText);
    Assert.Equal("user-2", safety.FetchedUserIds.Single());
  }

  [Fact]
  public async Task LoadPublicAsyncSkipsSafetyStateForSignedOutProfile()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanActOnUser);
    Assert.False(viewModel.CanViewUserTags);
    Assert.False(viewModel.IsMutedUser);
    Assert.False(viewModel.IsBlockedUser);
    Assert.Empty(safety.FetchedUserIds);
  }

  [Fact]
  public async Task LoadPublicAsyncSkipsSafetyStateForOwnProfile()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "alice", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "Alice");

    await viewModel.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanActOnUser);
    Assert.False(viewModel.CanViewUserTags);
    Assert.Empty(safety.FetchedUserIds);
  }

  [Fact]
  public async Task ToggleMuteAndBlockPersistUserBookmarkState()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsMutedUser);
    Assert.False(viewModel.IsBlockedUser);
    Assert.Equal("Mute", viewModel.MuteUserButtonText);
    Assert.Equal("Block", viewModel.BlockUserButtonText);
    Assert.Equal(
        [
          ("user-2", BookmarkPredicate.Mute, true),
          ("user-2", BookmarkPredicate.Block, true),
          ("user-2", BookmarkPredicate.Mute, false),
          ("user-2", BookmarkPredicate.Block, false),
        ],
        safety.BookmarkCalls);
  }

  [Fact]
  public async Task ToggleMuteUserRevertsLocalStateWhenBookmarkMutationFails()
  {
    var safety = new RecordingProfileSafetyService
    {
      SetException = new InvalidOperationException("Bookmark failed"),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsMutedUser);
    Assert.Equal("Bookmark failed", viewModel.ErrorMessage);
    Assert.Equal([("user-2", BookmarkPredicate.Mute, true)], safety.BookmarkCalls);
  }

  [Fact]
  public async Task LoadPublicAsyncClearsStaleSafetyErrorOnSuccessfulReload()
  {
    var safety = new RecordingProfileSafetyService
    {
      SetException = new InvalidOperationException("Bookmark failed"),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    safety.SetException = null;

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(["user-2", "user-2"], safety.FetchedUserIds);
  }

  [Fact]
  public async Task ToggleUserBookmarkIgnoresConcurrentDuplicateSubmissions()
  {
    var pendingBookmark = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var safety = new RecordingProfileSafetyService
    {
      PendingBookmark = pendingBookmark,
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    var firstToggle = viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);
    pendingBookmark.SetResult();
    await firstToggle;

    Assert.True(viewModel.IsMutedUser);
    Assert.False(viewModel.IsBlockedUser);
    Assert.Equal([("user-2", BookmarkPredicate.Mute, true)], safety.BookmarkCalls);
  }

  [Fact]
  public async Task ToggleUserBookmarkClearsStaleErrorOnNextAttempt()
  {
    var safety = new RecordingProfileSafetyService
    {
      SetException = new InvalidOperationException("Bookmark failed"),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    safety.SetException = null;

    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.True(viewModel.IsMutedUser);
  }

  [Fact]
  public async Task ReportUserAsyncSendsSelectedReasonAndResetsBusyState()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    var submitted = await viewModel.ReportUserAsync(
        "harassment",
        note: "threatening messages",
        turnstileToken: "turnstile-token",
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsReportingUser);
    Assert.True(submitted);
    Assert.Equal([("user-2", "harassment", "threatening messages", "turnstile-token")], safety.ReportCalls);
  }

  [Fact]
  public async Task ReportUserAsyncIgnoresConcurrentDuplicateSubmissions()
  {
    var pendingReport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var safety = new RecordingProfileSafetyService
    {
      PendingReport = pendingReport,
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    var firstReport = viewModel.ReportUserAsync("spam", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.ReportUserAsync("harassment", cancellationToken: TestContext.Current.CancellationToken);
    pendingReport.SetResult();
    await firstReport;

    Assert.False(viewModel.IsReportingUser);
    Assert.Equal([("user-2", "spam", null, null)], safety.ReportCalls);
  }

  [Fact]
  public async Task ReportUserAsyncClearsStaleErrorOnNextAttempt()
  {
    var safety = new RecordingProfileSafetyService
    {
      ReportException = new InvalidOperationException("Report failed"),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.ReportUserAsync(cancellationToken: TestContext.Current.CancellationToken);
    safety.ReportException = null;

    await viewModel.ReportUserAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal([("user-2", "spam", null, null), ("user-2", "spam", null, null)], safety.ReportCalls);
  }

  [Fact]
  public async Task ReportUserAsyncSurfacesServiceFailures()
  {
    var safety = new RecordingProfileSafetyService
    {
      ReportException = new InvalidOperationException("Report failed"),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    var submitted = await viewModel.ReportUserAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsReportingUser);
    Assert.False(submitted);
    Assert.Equal("Report failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SafetyActionsNoOpWhenProfileIsNotActionable()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.ToggleMuteUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);
    await viewModel.ReportUserAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Empty(safety.BookmarkCalls);
    Assert.Empty(safety.ReportCalls);
  }

  [Fact]
  public async Task SafetyActionsAreHiddenWhenUserIdIsEmpty()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User(string.Empty, "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanActOnUser);
    Assert.Empty(safety.FetchedUserIds);
  }

  [Fact]
  public void ConfigureProfileSafetyServiceRejectsNullService()
  {
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));

    Assert.Throws<ArgumentNullException>(() => viewModel.ConfigureProfileSafetyService(null!));
  }

  [Fact]
  public async Task UserTagsClearBeforeProfileChangesAndAfterOptionalRequestFailures()
  {
    const string tagsResponse = """
        {
          "results": [{ "id": "relation-1" }],
          "page_info": { "has_next_page": false },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1",
              "object_id": "user-tag-bot",
              "object_data": { "label": 42, "name": "Bot" },
              "votes_score_net": 3, "votes_count_up": 3, "votes_count_down": 0
            }
          },
          "election_votes": { "relation-1": { "choice": "confirm" } }
        }
        """;
    var handler = new RecordingHandler([
        new RecordedResponse(tagsResponse),
        new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.ConfigureUserTagClient(client);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.LoadUserTagsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new ProfileUserTagRow("relation-1", "Bot", 3, 0, ElectionVoteChoice.Confirm), Assert.Single(viewModel.UserTags));

    await viewModel.LoadPublicAsync("other-user", cancellationToken: TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.UserTags);

    await viewModel.LoadUserTagsAsync(TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.UserTags);
  }

  private static ProfileViewModel NewViewModel(
      User user,
      RecordingProfileSafetyService? safety = null,
      RecordingPostsService? posts = null,
      UserMetrics? metrics = null)
  {
    var viewModel = new ProfileViewModel(
        new RecordingSettingsService(user, metrics),
        new RecordingLandingPagesService(),
        posts ?? new RecordingPostsService(),
        new RecordingImageUploadService(),
        new AppConfig(
            new Uri("https://api.example.test"),
            "site-key",
            ImageBaseUrl: new Uri("https://images.example.test")));
    viewModel.ConfigureProfileSafetyService(safety ?? new RecordingProfileSafetyService());
    return viewModel;
  }

  private sealed class RecordingProfileSafetyService : IProfileSafetyService
  {
    public BookmarkPredicates Bookmarks { get; init; } = new();

    public Exception? SetException { get; set; }

    public Exception? ReportException { get; set; }

    public TaskCompletionSource? PendingReport { get; init; }

    public TaskCompletionSource? PendingBookmark { get; init; }

    public List<string> FetchedUserIds { get; } = [];

    public List<(string UserId, BookmarkPredicate Predicate, bool Active)> BookmarkCalls { get; } = [];

    public List<(string UserId, string Reason, string? Note, string? TurnstileToken)> ReportCalls { get; } = [];

    public Task<BookmarkPredicates> FetchUserBookmarksAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
      FetchedUserIds.Add(userId);
      return Task.FromResult(Bookmarks);
    }

    public Task SetUserBookmarkAsync(
        string userId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      BookmarkCalls.Add((userId, predicate, active));
      if (PendingBookmark is not null) return PendingBookmark.Task;
      return SetException is null ? Task.CompletedTask : Task.FromException(SetException);
    }

    public Task ReportUserAsync(
        string userId,
        string reason,
        string? note = null,
        string? turnstileToken = null,
        CancellationToken cancellationToken = default)
    {
      ReportCalls.Add((userId, reason, note, turnstileToken));
      if (PendingReport is not null) return PendingReport.Task;
      return ReportException is null ? Task.CompletedTask : Task.FromException(ReportException);
    }
  }

  private sealed class RecordingSettingsService(User user, UserMetrics? metrics = null) : ISettingsService
  {
    public Task<UserResponse> FetchUserAsync(
        string idOrSlug,
        bool includeBio = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(
            user,
            metrics ?? new UserMetrics(
                "user",
                user.Id,
                new UserMetricsCount(Reviews: 1, Discussions: 1, Comments: 1),
                new UserMetricsViewerCount(Reviews: 1, Discussions: 1, Comments: 1))));

    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyIdentityResponse> UpdateMyIdentityAsync(UpdateMyIdentityBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyProfileResponse> UpdateMyProfileAsync(string markdown, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<UserResponse> UpdateUserAsync(string idOrSlug, UpdateUserPrivacyBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(string idOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(string idOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DeleteUserResponse> DeleteUserAsync(string idOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(string label, string type, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProfileLinkResponse> CreateProfileLinkAsync(CreateProfileLinkBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(ReorderProfileLinksBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProfileLinkResponse> UpdateProfileLinkAsync(string id, UpdateProfileLinkBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MembershipPlansResponse> FetchMembershipPlansAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(MembershipCheckoutBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(MembershipPortalBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CancelMembershipAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class RecordingPostsService : IPostsService
  {
    public FetchPostsRequest? LastRequest { get; private set; }
    public int CallCount { get; private set; }
    public Func<int, FetchPostsRequest, Task<PostsFeedResponse>>? FetchPostsAsyncOverride { get; init; }

    public Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default)
    {
      LastRequest = request;
      CallCount++;
      return FetchPostsAsyncOverride?.Invoke(CallCount, request) ?? Task.FromResult(new PostsFeedResponse(
            [],
            new PageInfo(null, false, null),
            new Dictionary<string, Post>(),
            new Dictionary<string, User>(),
            new Dictionary<string, Community>()));
    }

    public Task<PostsFeedResponse> FetchFeedAsync(FetchPostsFeedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> CreatePostAsync(CreatePostBody body, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> CreateCommunityPostAsync(string communityIdOrSlug, CreatePostBody body, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class RecordingLandingPagesService : ILandingPagesService
  {
    public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> UpdateAsync(string pageId, UpdateLandingPageBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> ReplaceItemsAsync(string pageId, ReplaceLandingPageItemsBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class RecordingImageUploadService : IImageUploadService
  {
    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(CreateImageUploadUrlBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CompleteImageUploadResponse> CompleteAsync(string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ImageUploadStateResponse> FetchUploadStateAsync(string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
