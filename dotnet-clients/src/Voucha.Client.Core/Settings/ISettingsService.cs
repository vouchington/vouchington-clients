using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public partial interface ISettingsService
{
  Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default);

  Task<MyIdentityResponse> UpdateMyIdentityAsync(
      UpdateMyIdentityBody body,
      CancellationToken cancellationToken = default);

  Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default);

  Task<MyProfileResponse> UpdateMyProfileAsync(
      string markdown,
      CancellationToken cancellationToken = default);

  Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default);

  Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default);

  Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default);

  Task<UserResponse> FetchUserAsync(
      string idOrSlug,
      bool includeBio = false,
      CancellationToken cancellationToken = default);

  Task<UserResponse> UpdateUserAsync(
      string idOrSlug,
      UpdateUserPrivacyBody body,
      CancellationToken cancellationToken = default);

  Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default);

  Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default);

  Task<DeleteUserResponse> DeleteUserAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default);

  Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default);

  Task<ApiKeyCreationResponse> CreateApiKeyAsync(
      string label,
      string type,
      CancellationToken cancellationToken = default);

  Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default);

  Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default);

  Task<ProfileLinkResponse> CreateProfileLinkAsync(
      CreateProfileLinkBody body,
      CancellationToken cancellationToken = default);

  Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
      ReorderProfileLinksBody body,
      CancellationToken cancellationToken = default);

  Task<ProfileLinkResponse> UpdateProfileLinkAsync(
      string id,
      UpdateProfileLinkBody body,
      CancellationToken cancellationToken = default);

  Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default);

  Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default);

  Task<MembershipPlansResponse> FetchMembershipPlansAsync(
      CancellationToken cancellationToken = default);

  Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
      MembershipCheckoutBody body,
      CancellationToken cancellationToken = default);

  Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
      MembershipPortalBody body,
      CancellationToken cancellationToken = default);

  Task CancelMembershipAsync(CancellationToken cancellationToken = default);

  Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(
      CancellationToken cancellationToken = default);

  Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default);
}
