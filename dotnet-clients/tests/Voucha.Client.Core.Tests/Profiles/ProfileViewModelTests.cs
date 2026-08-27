using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ProfileViewModelTests
{
  [Fact]
  public async Task LoadOwnAsyncLoadsProfileAndHistory()
  {
    var settings = new RecordingSettingsService();
    var posts = new RecordingPostsService();
    var viewModel = NewViewModel(settings, posts);

    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanEdit);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Alice", viewModel.DisplayName);
    Assert.Equal("Hello", viewModel.BioMarkdown);
    Assert.Equal(new Uri("https://images.example.test/images/avatar-1?w=144"), viewModel.AvatarUrl);
    Assert.True(settings.LastIncludeBio);
    Assert.Equal("user-1", posts.LastRequest?.Creator);
    Assert.Equal("review,discussion,comment", posts.LastRequest?.PostTypes);
    Assert.Equal("new", posts.LastRequest?.Sort);
    Assert.Equal("First review", viewModel.HistoryItems.Single().Title);
  }

  [Fact]
  public async Task LoadPublicAsyncUsesInitialHistoryTabAndViewerCounts()
  {
    var settings = new RecordingSettingsService();
    var posts = new RecordingPostsService();
    var viewModel = NewViewModel(settings, posts);

    await viewModel.LoadPublicAsync(
        "alice",
        ProfileHistoryTab.Comments,
        TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanEdit);
    Assert.Equal("alice", settings.LastFetchedUserIdOrSlug);
    Assert.Equal("comment", posts.LastRequest?.PostTypes);
    Assert.Contains(viewModel.HistoryTabs, row =>
        row.Tab == ProfileHistoryTab.Comments && row.IsSelected && row.Count == 7);
  }

  [Fact]
  public async Task LoadPublicAsyncPrefersDisplayAccountName()
  {
    var settings = new RecordingSettingsService
    {
      ResponseUser = new User(
          "user-1",
          "alice",
          "Hello",
          Name: "Legacy Alice",
          VerifiedDisplayName: "Verified Alice",
          DisplayAccount: new UserDisplayAccount("oauth-1", "Display Alice")),
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService());

    await viewModel.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("Display Alice", viewModel.DisplayName);
  }

  [Fact]
  public async Task LoadOwnAsyncUsesSelectedPrivateProviderDisplayName()
  {
    var settings = new RecordingSettingsService
    {
      ResponseUser = new User(
          "user-1",
          "alice",
          "Hello",
          UseDisplayNameFrom: "google",
          GoogleAccount: new UserDisplayAccount("google-1", "Google Alice")),
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService());

    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Google Alice", viewModel.DisplayName);
  }

  [Fact]
  public async Task LoadOwnAsyncTreatsMissingPostsMapAsEmptyHistory()
  {
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (_, _) => Task.FromResult(new PostsFeedResponse(
          [new EntityReference(null, null, "post-1", null, null, null, null, null, null, null, null)],
          new PageInfo(null, false, null),
          null!,
          new Dictionary<string, User>(),
          new Dictionary<string, Community>())),
    };
    var viewModel = NewViewModel(new RecordingSettingsService(), posts);

    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.HistoryItems);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task LoadOwnAsyncDisablesEditingWhenProfileLoadFails()
  {
    var settings = new RecordingSettingsService
    {
      ThrowOnFetchUser = true,
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService());

    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanEdit);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("Profile failed", viewModel.ErrorMessage);
  }

  [Fact]
  public void ReportAvatarUploadFailureSetsFriendlyMessage()
  {
    var viewModel = NewViewModel(new RecordingSettingsService(), new RecordingPostsService());

    viewModel.ReportAvatarUploadFailure(string.Empty);

    Assert.Equal("Avatar upload failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SelectHistoryTabAsyncSurfacesHistoryLoadFailure()
  {
    var settings = new RecordingSettingsService();
    var posts = new RecordingPostsService();
    var viewModel = NewViewModel(settings, posts);

    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    posts.ThrowOnFetchPostsCall = 2;

    await viewModel.SelectHistoryTabAsync(ProfileHistoryTab.Comments, TestContext.Current.CancellationToken);

    Assert.Equal(ProfileHistoryTab.Comments, viewModel.SelectedHistoryTab);
    Assert.Equal("History failed", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("First review", viewModel.HistoryItems.Single().Title);
  }

  [Fact]
  public async Task SelectHistoryTabAsyncIgnoresStaleHistoryLoad()
  {
    var settings = new RecordingSettingsService();
    var posts = new RecordingPostsService();
    var pendingReviews = new TaskCompletionSource<PostsFeedResponse>();
    posts.FetchPostsAsyncOverride = (call, request) =>
        call == 2
            ? pendingReviews.Task
            : Task.FromResult(MakeHistoryResponse(
                request.PostTypes == "comment" ? "comment-1" : "post-1",
                request.PostTypes == "comment" ? "comment" : "review",
                request.PostTypes == "comment" ? "Current comment" : "First review"));
    var viewModel = NewViewModel(settings, posts);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    var staleReviewsTask = viewModel.SelectHistoryTabAsync(ProfileHistoryTab.Reviews, TestContext.Current.CancellationToken);
    await viewModel.SelectHistoryTabAsync(ProfileHistoryTab.Comments, TestContext.Current.CancellationToken);
    pendingReviews.SetResult(MakeHistoryResponse(
        "review-1",
        "review",
        "Stale review",
        "cursor-stale",
        true));
    await staleReviewsTask;

    Assert.Equal(ProfileHistoryTab.Comments, viewModel.SelectedHistoryTab);
    var item = Assert.Single(viewModel.HistoryItems);
    Assert.Equal("comment-1", item.Id);
    Assert.Equal("Current comment", item.Title);
    Assert.False(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task SaveBioAsyncUpdatesProfileMarkdown()
  {
    var settings = new RecordingSettingsService();
    var viewModel = NewViewModel(settings, new RecordingPostsService());
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await viewModel.SaveBioAsync("Updated bio", TestContext.Current.CancellationToken);

    Assert.Equal("Updated bio", settings.LastSavedMarkdown);
    Assert.Equal("Updated bio", viewModel.BioMarkdown);
    Assert.False(viewModel.IsSavingBio);
  }

  [Fact]
  public async Task UploadAvatarAsyncPatchesIdentityWithUploadedImage()
  {
    var settings = new RecordingSettingsService();
    var images = new RecordingImageUploadService();
    var viewModel = NewViewModel(settings, new RecordingPostsService(), images);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await using var stream = new MemoryStream([1, 2, 3]);
    await viewModel.UploadAvatarAsync(
        stream,
        "image/png",
        stream.Length,
        TestContext.Current.CancellationToken);

    Assert.Equal("image/png", images.ContentType);
    Assert.Equal(3, images.ContentLength);
    Assert.Equal(1, images.FetchStateCount);
    Assert.Equal("image-1", settings.LastIdentityUpdateBody?.ProfileImageId?.Value);
    Assert.Equal(new Uri("https://images.example.test/images/image-1?w=144"), viewModel.AvatarUrl);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task UploadAvatarAsyncWaitsForReadyStateBeforeSavingProfileImageId()
  {
    var settings = new RecordingSettingsService();
    var images = new RecordingImageUploadService
    {
      StateResponses =
      [
        new ImageUploadState("image-1", "complete", null, false, false),
        new ImageUploadState("image-2", "complete", null, true, false),
      ],
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService(), images);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await using var stream = new MemoryStream([1, 2, 3]);
    await viewModel.UploadAvatarAsync(
        stream,
        "image/png",
        stream.Length,
        TestContext.Current.CancellationToken);

    Assert.Equal(2, images.FetchStateCount);
    Assert.Equal("image-2", settings.LastIdentityUpdateBody?.ProfileImageId?.Value);
    Assert.Equal("image-2", viewModel.User?.ProfileImageId);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task UploadAvatarAsyncRejectsInvalidSize()
  {
    var images = new RecordingImageUploadService();
    var viewModel = NewViewModel(new RecordingSettingsService(), new RecordingPostsService(), images);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await using var stream = new MemoryStream();
    await viewModel.UploadAvatarAsync(
        stream,
        "image/png",
        contentLength: 0,
        TestContext.Current.CancellationToken);

    Assert.Equal("Choose an image up to 50 MB.", viewModel.ErrorMessage);
    Assert.Null(images.ContentType);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task UploadAvatarAsyncShowsUploadError()
  {
    var settings = new RecordingSettingsService();
    var images = new RecordingImageUploadService
    {
      StateResponses = [new ImageUploadState("image-1", "failed", "Scan failed", false, false)],
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService(), images);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await using var stream = new MemoryStream([1, 2, 3]);
    await viewModel.UploadAvatarAsync(
        stream,
        "image/png",
        stream.Length,
        TestContext.Current.CancellationToken);

    Assert.Equal("Scan failed", viewModel.ErrorMessage);
    Assert.Null(settings.LastIdentityUpdateBody);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task UploadAvatarAsyncShowsBlockedImageError()
  {
    var settings = new RecordingSettingsService();
    var images = new RecordingImageUploadService
    {
      StateResponses = [new ImageUploadState("image-1", "complete", null, false, true)],
    };
    var viewModel = NewViewModel(settings, new RecordingPostsService(), images);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await using var stream = new MemoryStream([1, 2, 3]);
    await viewModel.UploadAvatarAsync(
        stream,
        "image/png",
        stream.Length,
        TestContext.Current.CancellationToken);

    Assert.Equal("This image was blocked.", viewModel.ErrorMessage);
    Assert.Null(settings.LastIdentityUpdateBody);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task RemoveAvatarAsyncClearsIdentityImage()
  {
    var settings = new RecordingSettingsService();
    var viewModel = NewViewModel(settings, new RecordingPostsService());
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    await viewModel.RemoveAvatarAsync(TestContext.Current.CancellationToken);

    Assert.Null(settings.LastIdentityUpdateBody?.ProfileImageId?.Value);
    Assert.Null(viewModel.AvatarUrl);
  }

  private static ProfileViewModel NewViewModel(
      RecordingSettingsService settings,
      RecordingPostsService posts,
      IImageUploadService? images = null,
      RecordingLandingPagesService? landingPages = null) =>
      new(
          settings,
          landingPages ?? new RecordingLandingPagesService(),
          posts,
          images ?? new RecordingImageUploadService(),
          new AppConfig(
              new Uri("https://api.example.test"),
              "site-key",
              ImageBaseUrl: new Uri("https://images.example.test")));

  private static PostsFeedResponse MakeHistoryResponse(
      string id,
      string postType,
      string title,
      string? endCursor = null,
      bool hasNextPage = false) =>
      new(
          [new EntityReference(null, null, id, null, null, null, null, null, null, null, null)],
          new PageInfo(endCursor, hasNextPage, null),
          new Dictionary<string, Post> { [id] = new Post(id, postType, title, "Body", "user-1") },
          new Dictionary<string, User>(),
          new Dictionary<string, Community>());

  private sealed class RecordingSettingsService : ISettingsService
  {
    public User ResponseUser { get; set; } =
        new("user-1", "alice", "Hello", Name: "Alice", ProfileImageId: "avatar-1");

    public string? LastFetchedUserIdOrSlug { get; private set; }

    public bool LastIncludeBio { get; private set; }

    public string? LastSavedMarkdown { get; private set; }

    public UpdateMyIdentityBody? LastIdentityUpdateBody { get; private set; }

    public bool ThrowOnFetchUser { get; init; }

    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyIdentityResponse(new User(
            "user-1",
            "alice",
            Roles: [],
            EmailAddress: "alice@example.test",
            MembershipPlan: "free",
            ProfileImageId: "avatar-1")));

    public Task<MyIdentityResponse> UpdateMyIdentityAsync(
        UpdateMyIdentityBody body,
        CancellationToken cancellationToken = default)
    {
      LastIdentityUpdateBody = body;
      return Task.FromResult(new MyIdentityResponse(new User(
          "user-1",
          "alice",
          Roles: [],
          EmailAddress: "alice@example.test",
          MembershipPlan: "free",
          ProfileImageId: body.ProfileImageId?.Value)));
    }

    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", "Hello")));

    public Task<MyProfileResponse> UpdateMyProfileAsync(string markdown, CancellationToken cancellationToken = default)
    {
      LastSavedMarkdown = markdown;
      return Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", markdown)));
    }

    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionListResponse([], new PageInfo(null, false, null)));

    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<UserResponse> FetchUserAsync(
        string idOrSlug,
        bool includeBio = false,
        CancellationToken cancellationToken = default)
    {
      LastFetchedUserIdOrSlug = idOrSlug;
      LastIncludeBio = includeBio;
      if (ThrowOnFetchUser)
      {
        throw new InvalidOperationException("Profile failed");
      }

      return Task.FromResult(new UserResponse(
          ResponseUser,
          new UserMetrics(
              "user",
              "user-1",
              new UserMetricsCount(Reviews: 4, Discussions: 3, Comments: 2),
              new UserMetricsViewerCount(Reviews: 1, Discussions: 2, Comments: 7))));
    }

    public Task<UserResponse> UpdateUserAsync(
        string idOrSlug,
        UpdateUserPrivacyBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(new User("user-1", "alice", "Hello")));

    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(string idOrSlug, CancellationToken cancellationToken = default) => Task.FromResult<UserDataRequestResponse?>(null);
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
}
