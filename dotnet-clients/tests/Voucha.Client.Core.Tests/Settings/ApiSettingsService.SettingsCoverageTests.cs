using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ApiSettingsServiceSettingsCoverageTests
{
  [Fact]
  public async Task SettingsWrappersForwardIdentityApiKeyAndUserRequests()
  {
    var (service, handler) = CreateService(
        new RecordedResponse(JsonSerializer.Serialize(CreateUserResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateUserDataRequestCreationResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateDeleteUserResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateAuthSessionListResponse(), VouchaApiJson.Options)),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse(JsonSerializer.Serialize(CreateApiKeyListResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateApiKeyCreationResponse(), VouchaApiJson.Options)),
        new RecordedResponse("{}"));

    await service.UpdateUserAsync(
        "user-1",
        new UpdateUserPrivacyBody(
            LikesVisibility: "mutual_followers",
            UiLocale: JsonNullableString.FromString("fr")),
        TestContext.Current.CancellationToken);
    await service.CreateUserDataRequestAsync("user-1", TestContext.Current.CancellationToken);
    await service.DeleteUserAsync("user-1", TestContext.Current.CancellationToken);
    await service.FetchAuthSessionsAsync(TestContext.Current.CancellationToken);
    await service.DeleteAuthSessionAsync("session-1", TestContext.Current.CancellationToken);
    await service.RevokeAuthSessionsAsync(TestContext.Current.CancellationToken);
    await service.FetchApiKeysAsync(TestContext.Current.CancellationToken);
    await service.CreateApiKeyAsync("  Reader  ", "mcp", TestContext.Current.CancellationToken);
    await service.DeleteApiKeyAsync("api-key-1", TestContext.Current.CancellationToken);

    Assert.Equal(9, handler.Requests.Count);
    Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
    Assert.Equal("/api/v1/users/user-1", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"likes_visibility\":\"mutual_followers\"", handler.Requests[0].Body, StringComparison.Ordinal);
    Assert.Contains("\"ui_locale\":\"fr\"", handler.Requests[0].Body, StringComparison.Ordinal);

    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/users/user-1/data-request", handler.Requests[1].PathAndQuery);

    Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
    Assert.Equal("/api/v1/users/user-1", handler.Requests[2].PathAndQuery);

    Assert.Equal(HttpMethod.Get, handler.Requests[3].Method);
    Assert.Equal("/api/v1/auth/sessions", handler.Requests[3].PathAndQuery);

    Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    Assert.Equal("/api/v1/auth/sessions/session-1", handler.Requests[4].PathAndQuery);

    Assert.Equal(HttpMethod.Post, handler.Requests[5].Method);
    Assert.Equal("/api/v1/auth/sessions/revocations", handler.Requests[5].PathAndQuery);

    Assert.Equal(HttpMethod.Get, handler.Requests[6].Method);
    Assert.Equal("/api/v1/my/api-keys", handler.Requests[6].PathAndQuery);

    Assert.Equal(HttpMethod.Post, handler.Requests[7].Method);
    Assert.Equal("/api/v1/my/api-keys", handler.Requests[7].PathAndQuery);
    Assert.Contains("\"label\":\"Reader\"", handler.Requests[7].Body, StringComparison.Ordinal);
    Assert.Contains("\"type\":\"mcp\"", handler.Requests[7].Body, StringComparison.Ordinal);
    Assert.Contains("\"permissions\":[\"mcp-tools:read\",\"mcp-tools:write\"]", handler.Requests[7].Body, StringComparison.Ordinal);

    Assert.Equal(HttpMethod.Delete, handler.Requests[8].Method);
    Assert.Equal("/api/v1/my/api-keys/api-key-1", handler.Requests[8].PathAndQuery);
  }

  [Fact]
  public async Task SettingsWrappersForwardMembershipAndPushRequests()
  {
    var (service, handler) = CreateService(
        new RecordedResponse(JsonSerializer.Serialize(CreateMembershipPlansResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateCheckoutSessionResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreatePortalSessionResponse(), VouchaApiJson.Options)),
        new RecordedResponse("{}"),
        new RecordedResponse(JsonSerializer.Serialize(CreatePushSubscriptionListResponse(), VouchaApiJson.Options)),
        new RecordedResponse("{}"));

    await service.FetchMembershipPlansAsync(TestContext.Current.CancellationToken);
    await service.CreateMembershipCheckoutSessionAsync(
        new MembershipCheckoutBody(
            "price-1",
            new Uri("https://example.test/success"),
            new Uri("https://example.test/cancel")),
        TestContext.Current.CancellationToken);
    await service.CreateMembershipPortalSessionAsync(
        new MembershipPortalBody(new Uri("https://example.test/account")),
        TestContext.Current.CancellationToken);
    await service.CancelMembershipAsync(TestContext.Current.CancellationToken);
    await service.FetchPushSubscriptionsAsync(TestContext.Current.CancellationToken);
    await service.DeletePushSubscriptionAsync("push-1", TestContext.Current.CancellationToken);

    Assert.Equal(6, handler.Requests.Count);
    Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    Assert.Equal("/api/v1/memberships/plans", handler.Requests[0].PathAndQuery);

    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/memberships/checkout", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"price_id\":\"price-1\"", handler.Requests[1].Body, StringComparison.Ordinal);

    Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
    Assert.Equal("/api/v1/memberships/billing-portal-sessions", handler.Requests[2].PathAndQuery);
    Assert.Contains("\"return_url\":\"https://example.test/account\"", handler.Requests[2].Body, StringComparison.Ordinal);

    Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
    Assert.Equal("/api/v1/my/membership", handler.Requests[3].PathAndQuery);

    Assert.Equal(HttpMethod.Get, handler.Requests[4].Method);
    Assert.Equal("/api/v1/my/notifications/push-subscriptions", handler.Requests[4].PathAndQuery);

    Assert.Equal(HttpMethod.Delete, handler.Requests[5].Method);
    Assert.Equal("/api/v1/my/notifications/push-subscriptions/push-1", handler.Requests[5].PathAndQuery);
  }

  [Fact]
  public async Task SettingsWrappersForwardProfileLinkRequests()
  {
    var (service, handler) = CreateService(
        new RecordedResponse(JsonSerializer.Serialize(CreateProfileLinkListResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateProfileLinkResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateProfileLinkResponse(), VouchaApiJson.Options)),
        new RecordedResponse(JsonSerializer.Serialize(CreateProfileLinkListResponse(), VouchaApiJson.Options)),
        new RecordedResponse("{}"));

    await service.FetchProfileLinksAsync(TestContext.Current.CancellationToken);
    await service.CreateProfileLinkAsync(
        new CreateProfileLinkBody("github", Handle: "alice", Name: "GitHub"),
        TestContext.Current.CancellationToken);
    await service.UpdateProfileLinkAsync(
        "profile-link-1",
        new UpdateProfileLinkBody(Handle: "alice-dev"),
        TestContext.Current.CancellationToken);
    await service.ReorderProfileLinksAsync(
        new ReorderProfileLinksBody(["profile-link-2", "profile-link-1"]),
        TestContext.Current.CancellationToken);
    await service.DeleteProfileLinkAsync("profile-link-1", TestContext.Current.CancellationToken);

    Assert.Equal(5, handler.Requests.Count);
    Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    Assert.Equal("/api/v1/my/profile/links", handler.Requests[0].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/my/profile/links", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"link_type\":\"github\"", handler.Requests[1].Body, StringComparison.Ordinal);
    Assert.Contains("\"handle\":\"alice\"", handler.Requests[1].Body, StringComparison.Ordinal);
    Assert.Equal(HttpMethod.Patch, handler.Requests[2].Method);
    Assert.Equal("/api/v1/my/profile/links/profile-link-1", handler.Requests[2].PathAndQuery);
    Assert.Contains("\"handle\":\"alice-dev\"", handler.Requests[2].Body, StringComparison.Ordinal);
    Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
    Assert.Equal("/api/v1/my/profile/links/order", handler.Requests[3].PathAndQuery);
    Assert.Contains("\"ids\":[\"profile-link-2\",\"profile-link-1\"]", handler.Requests[3].Body, StringComparison.Ordinal);
    Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    Assert.Equal("/api/v1/my/profile/links/profile-link-1", handler.Requests[4].PathAndQuery);
  }

  private static (ApiSettingsService Service, RecordingHandler Handler) CreateService(
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiSettingsService(client), handler);
  }

  private static UserResponse CreateUserResponse() =>
      new(new User(
          "user-1",
          "alice",
          "markdown",
          FollowsVisibility: "followers",
          LikesVisibility: "nobody",
          DefaultPostPrivacy: "private",
          UiLocale: "en",
          ProcessingRestrictedAt: DateTimeOffset.Parse("2026-07-01T22:00:00Z"),
          ThirdPartyMarketing: true));

  private static UserDataRequestCreationResponse CreateUserDataRequestCreationResponse() =>
      new(
          "request-1",
          "ready",
          DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
          DateTimeOffset.Parse("2026-07-08T16:00:00-07:00"));

  private static DeleteUserResponse CreateDeleteUserResponse() => new(true);

  private static AuthSessionListResponse CreateAuthSessionListResponse() =>
      new(
          [CreateAuthSession()],
          new PageInfo(null, false, null));

  private static ApiKeyListResponse CreateApiKeyListResponse() =>
      new(
          [CreateApiKey()],
          new PageInfo(null, false, null));

  private static ApiKeyCreationResponse CreateApiKeyCreationResponse() =>
      new(CreateApiKey(), "raw-key");

  private static ProfileLinkListResponse CreateProfileLinkListResponse() =>
      new([CreateProfileLink()], new PageInfo(null, false, null));

  private static ProfileLinkResponse CreateProfileLinkResponse() => new(CreateProfileLink());

  private static MembershipPlansResponse CreateMembershipPlansResponse() =>
      MembershipPlansResponse.FromLegacyPlans(
          new Dictionary<string, IReadOnlyList<MembershipSku>>
          {
            ["pro"] = [new MembershipSku("sku-1", "pro", new Money(1500, "usd"), "month", "price-1")],
          });

  private static CheckoutSessionResponse CreateCheckoutSessionResponse() =>
      new(new CheckoutSession("checkout-1", new Uri("https://checkout")));

  private static PortalSessionResponse CreatePortalSessionResponse() =>
      new(new PortalSession(new Uri("https://portal")));

  private static WebPushSubscriptionListResponse CreatePushSubscriptionListResponse() =>
      new(
          [CreatePushSubscription()],
          new PageInfo(null, false, null));

  private static AuthSession CreateAuthSession() =>
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

  private static ApiKey CreateApiKey() =>
      new(
          "api-key-1",
          "user-1",
          "rk_abc123",
          "mcp",
          "Reader",
          ["mcp-tools:read", "mcp-tools:write"],
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
          null,
          null,
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

  private static ProfileLink CreateProfileLink() =>
      new(
          "profile-link-1",
          "user-1",
          "github",
          0,
          null,
          null,
          "alice",
          "GitHub",
          null,
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

  private static WebPushSubscription CreatePushSubscription() =>
      new(
          "__entity_type",
          "push-1",
          "user-1",
          "https://push.example.test",
          "p256dh",
          "auth",
          null,
          "device",
          null,
          null,
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"));
}
