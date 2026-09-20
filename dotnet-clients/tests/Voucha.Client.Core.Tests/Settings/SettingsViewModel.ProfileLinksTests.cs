using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class SettingsViewModelProfileLinksTests
{
  [Fact]
  public async Task SaveProfileLinkAsyncCreatesANewLinkFromTheDraft()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service)
    {
      ProfileLinkType = "github",
      ProfileLinkHandle = " alice ",
      ProfileLinkName = " GitHub ",
    };

    await viewModel.SaveProfileLinkAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(service.LastCreateProfileLinkBody);
    Assert.Equal("github", service.LastCreateProfileLinkBody!.LinkType);
    Assert.Equal("alice", service.LastCreateProfileLinkBody.Handle);
    Assert.Equal("GitHub", service.LastCreateProfileLinkBody.Name);
    Assert.Single(viewModel.ProfileLinks);
    Assert.Null(viewModel.EditingProfileLinkId);
  }

  [Fact]
  public async Task SaveProfileLinkAsyncUpdatesTheEditedLink()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    var link = CreateProfileLink("profile-link-1", "github", "alice", "GitHub");
    viewModel.EditProfileLink(link);
    viewModel.ProfileLinkHandle = "alice-dev";

    await viewModel.SaveProfileLinkAsync(TestContext.Current.CancellationToken);

    Assert.Equal("profile-link-1", service.LastUpdatedProfileLinkId);
    Assert.NotNull(service.LastUpdateProfileLinkBody);
    Assert.Equal("alice-dev", service.LastUpdateProfileLinkBody!.Handle);
    Assert.Null(viewModel.EditingProfileLinkId);
  }

  [Fact]
  public async Task DeleteProfileLinkAsyncRemovesTheLink()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.SaveProfileLinkAsync(TestContext.Current.CancellationToken);
    var link = Assert.Single(viewModel.ProfileLinks);

    await viewModel.DeleteProfileLinkAsync(link, TestContext.Current.CancellationToken);

    Assert.Equal("profile-link-1", service.LastDeletedProfileLinkId);
    Assert.Empty(viewModel.ProfileLinks);
  }

  [Fact]
  public async Task MoveProfileLinkAsyncSavesTheNewOrder()
  {
    var service = new RecordingSettingsService
    {
      ProfileLinkResults =
      [
        CreateProfileLink("profile-link-1", "github", "alice", "GitHub"),
        CreateProfileLink("profile-link-2", "url", null, "Site", "https://example.test"),
      ],
    };
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.MoveProfileLinkAsync(viewModel.ProfileLinks[1], -1, TestContext.Current.CancellationToken);

    Assert.Equal(["profile-link-2", "profile-link-1"], service.LastReorderProfileLinkIds);
  }

  private static ProfileLink CreateProfileLink(
      string id,
      string linkType,
      string? handle,
      string? name,
      string? url = null) =>
      new(id, "user-1", linkType, 0, null, url, handle, name, null,
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

  private sealed class RecordingSettingsService : ISettingsService
  {
    public CreateProfileLinkBody? LastCreateProfileLinkBody { get; private set; }

    public UpdateProfileLinkBody? LastUpdateProfileLinkBody { get; private set; }

    public string? LastUpdatedProfileLinkId { get; private set; }

    public string? LastDeletedProfileLinkId { get; private set; }

    public IReadOnlyList<string>? LastReorderProfileLinkIds { get; private set; }

    public IReadOnlyList<ProfileLink> ProfileLinkResults { get; set; } = [];

    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateIdentity());

    public Task<MyIdentityResponse> UpdateMyIdentityAsync(
        UpdateMyIdentityBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateIdentity());

    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", "Hello")));

    public Task<MyProfileResponse> UpdateMyProfileAsync(
        string markdown,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", markdown)));

    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionListResponse([CreateSession()], new PageInfo(null, false, null)));

    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<UserResponse> FetchUserAsync(
        string idOrSlug,
        bool includeBio = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(new User("user-1", "alice", "markdown")));

    public Task<UserResponse> UpdateUserAsync(
        string idOrSlug,
        UpdateUserPrivacyBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(new User("user-1", "alice", "markdown")));

    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDataRequestResponse?>(null);

    public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserDataRequestCreationResponse(
            "request-1",
            "ready",
            DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
            null));

    public Task<DeleteUserResponse> DeleteUserAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeleteUserResponse(false));

    public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApiKeyListResponse([], new PageInfo(null, false, null)));

    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
        string label,
        string type,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApiKeyCreationResponse(CreateApiKey(), "raw-key"));

    public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse(ProfileLinkResults, new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> CreateProfileLinkAsync(
        CreateProfileLinkBody body,
        CancellationToken cancellationToken = default)
    {
      LastCreateProfileLinkBody = body;
      var link = CreateProfileLink("profile-link-1", body.LinkType, body.Handle, body.Name, body.Url);
      ProfileLinkResults = [.. ProfileLinkResults, link];
      return Task.FromResult(new ProfileLinkResponse(link));
    }

    public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
        ReorderProfileLinksBody body,
        CancellationToken cancellationToken = default)
    {
      LastReorderProfileLinkIds = body.Ids;
      ProfileLinkResults = body.Ids
          .Select(id => ProfileLinkResults.Single(link => link.Id == id))
          .ToArray();
      return Task.FromResult(new ProfileLinkListResponse(ProfileLinkResults, new PageInfo(null, false, null)));
    }

    public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
        string id,
        UpdateProfileLinkBody body,
        CancellationToken cancellationToken = default)
    {
      LastUpdatedProfileLinkId = id;
      LastUpdateProfileLinkBody = body;
      var updated = CreateProfileLink(id, "github", body.Handle, body.Name, body.Url);
      return Task.FromResult(new ProfileLinkResponse(updated));
    }

    public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default)
    {
      LastDeletedProfileLinkId = id;
      ProfileLinkResults = ProfileLinkResults.Where(link => link.Id != id).ToArray();
      return Task.CompletedTask;
    }

    public Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<MembershipResponse?>(null);

    public Task<MembershipPlansResponse> FetchMembershipPlansAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MembershipPlansResponse([]));

    public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
        MembershipCheckoutBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new CheckoutSessionResponse(new CheckoutSession("checkout-1", new Uri("https://checkout"))));

    public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
        MembershipPortalBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PortalSessionResponse(new PortalSession(new Uri("https://portal"))));

    public Task CancelMembershipAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebPushSubscriptionListResponse([], new PageInfo(null, false, null)));

    public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static MyIdentityResponse CreateIdentity() =>
        new(new User("user-1", "alice", Roles: [], EmailAddress: "alice@example.com", MembershipPlan: null));

    private static ApiKey CreateApiKey() =>
        new("api-key-1", "user-1", "rk_abc123", "rss", "Reader", [],
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"), null, null,
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

    private static ProfileLink CreateProfileLink(
        string id,
        string linkType,
        string? handle,
        string? name,
        string? url = null) =>
        new(id, "user-1", linkType, 0, null, url, handle, name, null,
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

    private static AuthSession CreateSession() =>
        new(
            "session-1",
            "device-1",
            "MacBook Pro",
            "Safari",
            "203.0.113.8",
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T13:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T12:00:00Z"),
            true);
  }
}
