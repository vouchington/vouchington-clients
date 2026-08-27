using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class ApiSettingsService : ISettingsService, INotificationPreferencesService
{
  private readonly VouchaApiClient client;

  public ApiSettingsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
      client.FetchMyIdentityAsync(cancellationToken);

  public Task<MyIdentityResponse> UpdateMyIdentityAsync(
      UpdateMyIdentityBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdateMyIdentityAsync(body, cancellationToken);

  public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
      client.FetchMyProfileAsync(cancellationToken);

  public Task<MyProfileResponse> UpdateMyProfileAsync(
      string markdown,
      CancellationToken cancellationToken = default) =>
      client.UpdateMyProfileAsync(markdown, cancellationToken);

  public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
      client.FetchAuthSessionsAsync(cancellationToken);

  public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeleteAuthSessionAsync(id, cancellationToken);

  public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) =>
      client.RevokeAuthSessionsAsync(cancellationToken);

  public Task<UserResponse> FetchUserAsync(
      string idOrSlug,
      bool includeBio = false,
      CancellationToken cancellationToken = default) =>
      client.FetchUserAsync(idOrSlug, includeBio, cancellationToken);

  public Task<UserResponse> UpdateUserAsync(
      string idOrSlug,
      UpdateUserPrivacyBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdateUserAsync(idOrSlug, body, cancellationToken);

  public Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(
      CancellationToken cancellationToken = default) =>
      client.FetchEmailPreferencesAsync(cancellationToken);

  public Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(
      UpdateEmailPreferencesBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdateEmailPreferencesAsync(body, cancellationToken);

  public async Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default)
  {
    try
    {
      return await client.FetchUserDataRequestAsync(idOrSlug, cancellationToken).ConfigureAwait(false);
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      return null;
    }
  }

  public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.CreateUserDataRequestAsync(idOrSlug, cancellationToken);

  public Task<DeleteUserResponse> DeleteUserAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.DeleteUserAsync(idOrSlug, cancellationToken);

  public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) =>
      client.FetchApiKeysAsync(cancellationToken);

  public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
      string label,
      string type,
      CancellationToken cancellationToken = default) =>
      client.CreateApiKeyAsync(
          new CreateApiKeyBody(
              (label ?? throw new ArgumentNullException(nameof(label))).Trim(),
              type == "mcp" ? ["mcp-tools:read", "mcp-tools:write"] : ["rss-feeds:read"],
              type == "mcp" ? "mcp" : "rss"),
          cancellationToken);

  public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeleteApiKeyAsync(id, cancellationToken);

  public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
      client.FetchProfileLinksAsync(cancellationToken);

  public Task<ProfileLinkResponse> CreateProfileLinkAsync(
      CreateProfileLinkBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateProfileLinkAsync(body, cancellationToken);

  public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
      ReorderProfileLinksBody body,
      CancellationToken cancellationToken = default) =>
      client.ReorderProfileLinksAsync(body, cancellationToken);

  public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
      string id,
      UpdateProfileLinkBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdateProfileLinkAsync(id, body, cancellationToken);

  public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeleteProfileLinkAsync(id, cancellationToken);

  public async Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return await client.FetchMembershipAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      return null;
    }
  }

  public Task<MembershipPlansResponse> FetchMembershipPlansAsync(CancellationToken cancellationToken = default) =>
      client.FetchMembershipPlansAsync(cancellationToken);

  public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
      MembershipCheckoutBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateMembershipCheckoutSessionAsync(body, cancellationToken);

  public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
      MembershipPortalBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateMembershipPortalSessionAsync(body, cancellationToken);

  public Task CancelMembershipAsync(CancellationToken cancellationToken = default) =>
      client.CancelMembershipAsync(cancellationToken);

  public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(
      CancellationToken cancellationToken = default) =>
      client.FetchPushSubscriptionsAsync(cancellationToken);

  public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeletePushSubscriptionAsync(id, cancellationToken);
}
