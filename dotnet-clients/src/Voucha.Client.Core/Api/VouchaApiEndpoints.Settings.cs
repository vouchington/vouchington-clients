namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest EmailPreferences() => Get("/api/v1/my/email-preferences");

  public static ApiRequest UpdateEmailPreferences(UpdateEmailPreferencesBody body) =>
      new(HttpMethod.Patch, "/api/v1/my/email-preferences") { Body = body };

  public static ApiRequest EmailAddresses(string? after = null, int? limit = null) =>
      Get("/api/v1/my/email-addresses", Query(("limit", limit), ("after", after)));

  public static ApiRequest RequestEmailAddressVerification(string emailAddress) =>
      new(HttpMethod.Post, "/api/v1/my/email-addresses")
      {
        Body = new RequestEmailAddressVerificationBody(emailAddress),
      };

  public static ApiRequest VerifyEmailAddress(string emailAddress, string token) =>
      new(HttpMethod.Post, $"/api/v1/my/email-addresses/{Path(emailAddress)}/verifications")
      {
        Body = new VerifyEmailAddressBody(token),
      };

  public static ApiRequest UpdateMyIdentity(UpdateMyIdentityBody body) =>
      new(HttpMethod.Patch, "/api/v1/my/identity") { Body = body };

  public static ApiRequest User(string idOrSlug, bool includeBio = false) =>
      Get(
          $"/api/v1/users/{Path(idOrSlug)}",
          Query(("include_bio", includeBio ? "1" : null)));

  public static ApiRequest UpdateUser(string idOrSlug, UpdateUserPrivacyBody body) =>
      new(HttpMethod.Patch, $"/api/v1/users/{Path(idOrSlug)}") { Body = body };

  public static ApiRequest DeleteUser(string idOrSlug) =>
      new(HttpMethod.Delete, $"/api/v1/users/{Path(idOrSlug)}");

  public static ApiRequest CreateUserDataRequest(string idOrSlug) =>
      new(HttpMethod.Post, $"/api/v1/users/{Path(idOrSlug)}/data-request");

  public static ApiRequest UserDataRequest(string idOrSlug) =>
      Get($"/api/v1/users/{Path(idOrSlug)}/data-request");

  public static ApiRequest ApiKeys(string? after = null, int? limit = null) =>
      Get("/api/v1/my/api-keys", Query(("limit", limit), ("after", after)));

  public static ApiRequest CreateApiKey(CreateApiKeyBody body) =>
      new(HttpMethod.Post, "/api/v1/my/api-keys") { Body = body };

  public static ApiRequest DeleteApiKey(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/api-keys/{Path(id)}");

  public static ApiRequest ProfileLinks() => Get("/api/v1/my/profile/links");

  public static ApiRequest CreateProfileLink(CreateProfileLinkBody body) =>
      new(HttpMethod.Post, "/api/v1/my/profile/links") { Body = body };

  public static ApiRequest ReorderProfileLinks(ReorderProfileLinksBody body) =>
      new(HttpMethod.Put, "/api/v1/my/profile/links/order") { Body = body };

  public static ApiRequest UpdateProfileLink(string id, UpdateProfileLinkBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/profile/links/{Path(id)}") { Body = body };

  public static ApiRequest DeleteProfileLink(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/profile/links/{Path(id)}");

  public static ApiRequest MembershipMe() => Get("/api/v1/memberships/me");

  public static ApiRequest MembershipCheckout(MembershipCheckoutBody body) =>
      new(HttpMethod.Post, "/api/v1/memberships/checkout") { Body = body };

  public static ApiRequest MembershipPortal(MembershipPortalBody body) =>
      new(HttpMethod.Post, "/api/v1/memberships/billing-portal-sessions") { Body = body };

  public static ApiRequest CancelMembership() => new(HttpMethod.Delete, "/api/v1/my/membership");

  public static ApiRequest PushSubscriptions(string? after = null, int? limit = null) =>
      Get(
          "/api/v1/my/notifications/push-subscriptions",
          Query(("limit", limit), ("after", after)));

  public static ApiRequest DeletePushSubscription(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/notifications/push-subscriptions/{Path(id)}");
}
