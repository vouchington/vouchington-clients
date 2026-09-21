using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  private sealed partial class RecordingSettingsService : ISettingsService
  {
    public int FetchMyIdentityCount { get; private set; }

    public int FetchApiKeysCount { get; private set; }

    public int CancelMembershipCount { get; private set; }

    public int RevokeAuthSessionsCount { get; private set; }

    public string? LastCreatedUserDataRequestUserId { get; private set; }

    public string? LastCreatedApiKeyLabel { get; private set; }

    public string? LastCreatedApiKeyType { get; private set; }

    public string? LastDeletedApiKeyId { get; private set; }

    public string? LastDeletedPushSubscriptionId { get; private set; }

    public int DeleteUserCallCount { get; private set; }

    public string? LastDeletedAuthSessionId { get; private set; }

    public UpdateUserPrivacyBody? LastPrivacyUpdateBody { get; private set; }

    public MembershipCheckoutBody? LastCheckoutBody { get; private set; }

    public MembershipPortalBody? LastPortalBody { get; private set; }

    public UserDataRequestResponse? UserDataRequestResult { get; set; } = CreateDataRequest();
    public string? UserUiLocale { get; set; } = "en";

    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default)
    {
      FetchMyIdentityCount++;
      return Task.FromResult(CreateIdentity());
    }

    public Task<MyIdentityResponse> UpdateMyIdentityAsync(
        UpdateMyIdentityBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateIdentity());

    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateProfile());

    public Task<MyProfileResponse> UpdateMyProfileAsync(
        string markdown,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateProfile());

    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionListResponse([CreateSession()], new PageInfo(null, false, null)));

    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default)
    {
      LastDeletedAuthSessionId = id;
      return Task.CompletedTask;
    }

    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default)
    {
      RevokeAuthSessionsCount++;
      return Task.CompletedTask;
    }

    public Task<UserResponse> FetchUserAsync(
        string idOrSlug,
        bool includeBio = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateUser());

    public Task<UserResponse> UpdateUserAsync(
        string idOrSlug,
        UpdateUserPrivacyBody body,
        CancellationToken cancellationToken = default)
    {
      LastPrivacyUpdateBody = body;
      return Task.FromResult(CreateUser());
    }

    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDataRequestResponse?>(UserDataRequestResult);

    public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default)
    {
      LastCreatedUserDataRequestUserId = idOrSlug;
      return Task.FromResult(new UserDataRequestCreationResponse(
          "request-1",
          "ready",
          DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
          DateTimeOffset.Parse("2027-07-08T16:00:00-07:00")));
    }

    public Task<DeleteUserResponse> DeleteUserAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default)
    {
      DeleteUserCallCount++;
      return Task.FromResult(new DeleteUserResponse(true));
    }

    public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default)
    {
      FetchApiKeysCount++;
      return Task.FromResult(new ApiKeyListResponse([CreateApiKey()], new PageInfo(null, false, null)));
    }

    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
        string label,
        string type,
        CancellationToken cancellationToken = default)
    {
      LastCreatedApiKeyLabel = label;
      LastCreatedApiKeyType = type;
      return Task.FromResult(new ApiKeyCreationResponse(CreateApiKey(), "raw-key"));
    }

    public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default)
    {
      LastDeletedApiKeyId = id;
      return Task.CompletedTask;
    }

    public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse([], new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> CreateProfileLinkAsync(
        CreateProfileLinkBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkResponse(CreateProfileLink()));

    public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
        ReorderProfileLinksBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse([], new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
        string id,
        UpdateProfileLinkBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkResponse(CreateProfileLink()));

    public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<MembershipResponse?>(new MembershipResponse(CreateMembership()));

    public Task<MembershipPlansResponse> FetchMembershipPlansAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new MembershipPlansResponse(
                [new MembershipCatalogProduct(
                    "sku-1",
                    "pro",
                    "month",
                    [new MembershipCatalogProvider(
                        "stripe",
                        "test",
                        "voucha-web",
                        "price-1",
                        null,
                        null,
                        null,
                        new Money(1500, "usd"))])]));

    public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
        MembershipCheckoutBody body,
        CancellationToken cancellationToken = default)
    {
      LastCheckoutBody = body;
      return Task.FromResult(new CheckoutSessionResponse(new CheckoutSession("checkout-1", new Uri("https://checkout"))));
    }

    public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
        MembershipPortalBody body,
        CancellationToken cancellationToken = default)
    {
      LastPortalBody = body;
      return Task.FromResult(new PortalSessionResponse(new PortalSession(new Uri("https://portal"))));
    }

    public Task CancelMembershipAsync(CancellationToken cancellationToken = default)
    {
      CancelMembershipCount++;
      return Task.CompletedTask;
    }

    public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebPushSubscriptionListResponse([CreatePushSubscription()], new PageInfo(null, false, null)));

    public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default)
    {
      LastDeletedPushSubscriptionId = id;
      return Task.CompletedTask;
    }

  }
}
