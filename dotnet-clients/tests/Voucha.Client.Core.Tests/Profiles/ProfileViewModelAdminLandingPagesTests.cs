using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ProfileViewModelAdminLandingPagesTests
{
  [Fact]
  public async Task LoadAdminLandingPagesAsyncLoadsAdminOnlyLandingPages()
  {
    var landingPages = new Voucha.Client.Core.Tests.LandingPages.RecordingLandingPagesService
    {
      AdminLandingPagesResponse = new LandingPagesResponse([
          new LandingPage(
              "page-1",
              "user-1",
              "Landing Page",
              "Subtitle",
              "landing-page",
              true,
              DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
              DateTimeOffset.Parse("2026-06-28T10:00:00Z")),
        ], new PageInfo(null, false, null)),
    };
    var viewModel = new Voucha.Client.Core.Profiles.ProfileViewModel(
        new NoOpSettingsService(),
        landingPages,
        new NoOpPostsService(),
        new NoOpImageUploadService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", ImageBaseUrl: new Uri("https://images.example.test")));

    await viewModel.LoadAdminLandingPagesAsync("user-1", true, TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanViewAdminLandingPages);
    Assert.Equal("user-1", landingPages.LastAdminUserId);
    Assert.Single(viewModel.AdminLandingPages);
    Assert.Equal("page-1", viewModel.AdminLandingPages[0].Id);
  }

  [Fact]
  public async Task LoadAdminLandingPagesAsyncHidesSectionForNonAdmins()
  {
    var viewModel = new Voucha.Client.Core.Profiles.ProfileViewModel(
        new NoOpSettingsService(),
        new RecordingLandingPagesService(),
        new NoOpPostsService(),
        new NoOpImageUploadService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", ImageBaseUrl: new Uri("https://images.example.test")));

    await viewModel.LoadAdminLandingPagesAsync("user-1", false, TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanViewAdminLandingPages);
    Assert.Empty(viewModel.AdminLandingPages);
  }

  private sealed class NoOpSettingsService : ISettingsService
  {
    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyIdentityResponse> UpdateMyIdentityAsync(UpdateMyIdentityBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MyProfileResponse> UpdateMyProfileAsync(string markdown, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<UserResponse> FetchUserAsync(string idOrSlug, bool includeBio = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

  private sealed class NoOpPostsService : IPostsService
  {
    public Task<PostsFeedResponse> FetchFeedAsync(FetchPostsFeedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostsFeedResponse> FetchPostsAsync(FetchPostsRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PostsFeedResponse(
            [],
            new PageInfo(null, false, null),
            null!,
            new Dictionary<string, User>(),
            new Dictionary<string, Community>()));
    public Task<PostMutationResponse> CreatePostAsync(CreatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> CreateCommunityPostAsync(string communityIdOrSlug, CreatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class NoOpImageUploadService : IImageUploadService
  {
    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(CreateImageUploadUrlBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CompleteImageUploadResponse> CompleteAsync(string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ImageUploadStateResponse> FetchUploadStateAsync(string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
