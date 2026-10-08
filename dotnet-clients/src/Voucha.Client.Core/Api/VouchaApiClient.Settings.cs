namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<EmailAddressListResponse> FetchEmailAddressesAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailAddressListResponse>(VouchaApiEndpoints.EmailAddresses(), cancellationToken);

  public Task<EmailAddressListResponse> FetchEmailAddressesPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailAddressListResponse>(VouchaApiEndpoints.EmailAddresses(after, limit), cancellationToken);

  public Task<EmailAddressRequestResponse> RequestEmailAddressVerificationAsync(
      string emailAddress,
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailAddressRequestResponse>(
          VouchaApiEndpoints.RequestEmailAddressVerification(emailAddress),
          cancellationToken);

  public Task<EmailAddressListResponse> VerifyEmailAddressAsync(
      string emailAddress,
      string token,
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailAddressListResponse>(
          VouchaApiEndpoints.VerifyEmailAddress(emailAddress, token),
          cancellationToken);

  public Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailPreferencesResponse>(VouchaApiEndpoints.EmailPreferences(), cancellationToken);

  public Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(
      UpdateEmailPreferencesBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<EmailPreferencesResponse>(VouchaApiEndpoints.UpdateEmailPreferences(body), cancellationToken);

  public Task<UserResponse> FetchUserAsync(
      string idOrSlug,
      bool includeBio = false,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserResponse>(VouchaApiEndpoints.User(idOrSlug, includeBio), cancellationToken);

  public Task<UserResponse> UpdateUserAsync(
      string idOrSlug,
      UpdateUserPrivacyBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserResponse>(VouchaApiEndpoints.UpdateUser(idOrSlug, body), cancellationToken);

  public Task<MyIdentityResponse> UpdateMyIdentityAsync(
      UpdateMyIdentityBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<MyIdentityResponse>(VouchaApiEndpoints.UpdateMyIdentity(body), cancellationToken);

  public Task<MyProfileResponse> UpdateMyProfileAsync(
      string markdown,
      CancellationToken cancellationToken = default) =>
      SendAsync<MyProfileResponse>(VouchaApiEndpoints.UpdateProfile(markdown), cancellationToken);

  public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
      SendAsync<AuthSessionListResponse>(VouchaApiEndpoints.AuthSessions(), cancellationToken);

  public Task<AuthSessionListResponse> FetchAuthSessionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<AuthSessionListResponse>(VouchaApiEndpoints.AuthSessions(after, limit), cancellationToken);

  public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteAuthSession(id), cancellationToken);

  public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.RevokeAuthSessions(), cancellationToken);

  public Task<BeginBlueskyAccountLinkResponse> BeginBlueskyAccountLinkAsync(
      string handle,
      CancellationToken cancellationToken = default) =>
      SendAsync<BeginBlueskyAccountLinkResponse>(
          VouchaApiEndpoints.BeginBlueskyAccountLink(handle),
          cancellationToken);

  public Task DisconnectBlueskyAccountAsync(CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DisconnectBlueskyAccount(), cancellationToken);

  public Task<UserDataRequestResponse> FetchUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserDataRequestResponse>(VouchaApiEndpoints.UserDataRequest(idOrSlug), cancellationToken);

  public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserDataRequestCreationResponse>(
          VouchaApiEndpoints.CreateUserDataRequest(idOrSlug),
          cancellationToken);

  public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) =>
      SendAsync<ApiKeyListResponse>(VouchaApiEndpoints.ApiKeys(), cancellationToken);

  public Task<ApiKeyListResponse> FetchApiKeysPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<ApiKeyListResponse>(VouchaApiEndpoints.ApiKeys(after, limit), cancellationToken);

  public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
      CreateApiKeyBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ApiKeyCreationResponse>(VouchaApiEndpoints.CreateApiKey(body), cancellationToken);

  public Task<ApiKeyCreationResponse> RotateApiKeyAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync<ApiKeyCreationResponse>(VouchaApiEndpoints.RotateApiKey(id), cancellationToken);

  public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteApiKey(id), cancellationToken);

  public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
      SendAsync<ProfileLinkListResponse>(VouchaApiEndpoints.ProfileLinks(), cancellationToken);

  public Task<ProfileLinkResponse> CreateProfileLinkAsync(
      CreateProfileLinkBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ProfileLinkResponse>(VouchaApiEndpoints.CreateProfileLink(body), cancellationToken);

  public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
      ReorderProfileLinksBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ProfileLinkListResponse>(VouchaApiEndpoints.ReorderProfileLinks(body), cancellationToken);

  public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
      string id,
      UpdateProfileLinkBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ProfileLinkResponse>(VouchaApiEndpoints.UpdateProfileLink(id, body), cancellationToken);

  public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteProfileLink(id), cancellationToken);

  public Task<MembershipResponse> FetchMembershipAsync(CancellationToken cancellationToken = default) =>
      SendAsync<MembershipResponse>(VouchaApiEndpoints.MembershipMe(), cancellationToken);

  public Task<MembershipPlansResponse> FetchMembershipPlansAsync(CancellationToken cancellationToken = default) =>
      SendAsync<MembershipPlansResponse>(VouchaApiEndpoints.MembershipPlans(), cancellationToken);

  public Task<GrantMembershipResponse> GrantMembershipAsync(
      GrantMembershipBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<GrantMembershipResponse>(VouchaApiEndpoints.GrantMembership(body), cancellationToken);

  public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
      MembershipCheckoutBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CheckoutSessionResponse>(VouchaApiEndpoints.MembershipCheckout(body), cancellationToken);

  public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
      MembershipPortalBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<PortalSessionResponse>(VouchaApiEndpoints.MembershipPortal(body), cancellationToken);

  public Task CancelMembershipAsync(CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.CancelMembership(), cancellationToken);

  public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<WebPushSubscriptionListResponse>(VouchaApiEndpoints.PushSubscriptions(), cancellationToken);

  public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsPageAsync(
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<WebPushSubscriptionListResponse>(VouchaApiEndpoints.PushSubscriptions(after, limit), cancellationToken);

  public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeletePushSubscription(id), cancellationToken);

  public Task<DeleteUserResponse> DeleteUserAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<DeleteUserResponse>(VouchaApiEndpoints.DeleteUser(idOrSlug), cancellationToken);
}
