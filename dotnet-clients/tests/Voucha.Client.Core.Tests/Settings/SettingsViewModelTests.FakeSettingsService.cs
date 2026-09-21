using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;
namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  private partial class FakeSettingsService : ISettingsService, INotificationPreferencesService
  {
    public string? LastFetchedUserIdOrSlug { get; private set; }

    public string? LastUpdatedUserIdOrSlug { get; private set; }

    public string? LastDeletedUserIdOrSlug { get; private set; }

    public UpdateMyIdentityBody? LastIdentityUpdateBody { get; private set; }

    public UpdateUserPrivacyBody? LastPrivacyUpdateBody { get; private set; }
    public UpdateEmailPreferencesBody? LastEmailPreferencesUpdateBody { get; private set; }
    public EmailPreferences EmailPreferences { get; set; } = new(true, "weekly", true, "weekly", "daily", [1, 2, 3, 4, 5], "09:00", null);

    public DeleteUserResponse DeleteUserResponse { get; set; } = new(false);

    public MembershipResponse? MembershipResponse { get; set; } = new MembershipResponse(CreateMembership());

    public MembershipPlansResponse MembershipPlansResponse { get; set; } =
        new(
        [
          Product("sku-1", "plus", "month", new Money(500, "usd"), "price-1"),
          Product("sku-2", "pro", "month", new Money(1500, "usd"), "price-2"),
          Product("sku-3", "pro", "year", new Money(15000, "usd"), "price-3"),
        ], CreateBenefitCatalog());

    private static MembershipCatalogProduct Product(string id, string plan, string interval, Money price, string priceId) =>
        new(id, plan, interval, [new MembershipCatalogProvider("stripe", "test", "voucha-web", priceId, null, null, null, price)]);

    private static MembershipBenefitCatalog CreateBenefitCatalog() => new(1,
    [
      new MembershipBenefitGroup("contribute",
      [
        new MembershipBenefit(
            "public_contribution_access",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("access", Access: "after_wait"),
              ["plus"] = new("access", Access: "immediate"),
              ["pro"] = new("access", Access: "immediate"),
            }),
        new MembershipBenefit(
            "contribution_capacity",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("level", Level: "standard"),
              ["plus"] = new("level", Level: "more"),
              ["pro"] = new("level", Level: "most"),
            }),
        new MembershipBenefit(
            "automatic_post_topics",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("level", Level: "none"),
              ["plus"] = new("level", Level: "more"),
              ["pro"] = new("level", Level: "most"),
            }),
      ]),
      new MembershipBenefitGroup("research",
      [
        new MembershipBenefit(
            "ugc_downvote_counts",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("availability", Included: false),
              ["plus"] = new("availability", Included: true),
              ["pro"] = new("availability", Included: true),
            }),
      ]),
      new MembershipBenefitGroup("communities",
      [
        new MembershipBenefit(
            "community_agent_rules",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("quantity", Quantity: 0),
              ["plus"] = new("quantity", Quantity: 3),
              ["pro"] = new("quantity", Quantity: 10),
            }),
      ]),
      new MembershipBenefitGroup("support",
      [
        new MembershipBenefit(
            "support_service_level",
            ["card", "comparison"],
            new Dictionary<string, MembershipBenefitValue>
            {
              ["free"] = new("level", Level: "standard"),
              ["plus"] = new("level", Level: "priority"),
              ["pro"] = new("level", Level: "highest_priority"),
            }),
      ]),
    ]);

    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateIdentity());

    public Task<MyIdentityResponse> UpdateMyIdentityAsync(
        UpdateMyIdentityBody body,
        CancellationToken cancellationToken = default)
    {
      LastIdentityUpdateBody = body;
      return Task.FromResult(CreateIdentity());
    }

    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateProfile());

    public Task<MyProfileResponse> UpdateMyProfileAsync(
        string markdown,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateProfile());

    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionListResponse([CreateSession()], InitialSessionPage));

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
      return Task.FromResult(CreateUser());
    }

    public Task<UserResponse> UpdateUserAsync(
        string idOrSlug,
        UpdateUserPrivacyBody body,
        CancellationToken cancellationToken = default)
    {
      LastUpdatedUserIdOrSlug = idOrSlug;
      LastPrivacyUpdateBody = body;
      return Task.FromResult(CreateUser());
    }

    public virtual Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EmailPreferencesResponse(EmailPreferences));

    public virtual Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(
        UpdateEmailPreferencesBody body,
        CancellationToken cancellationToken = default)
    {
      LastEmailPreferencesUpdateBody = body;
      EmailPreferences = EmailPreferences with
      {
        EngagementEmailsEnabled = body.EngagementEmailsEnabled ?? EmailPreferences.EngagementEmailsEnabled,
        NewsDigestFrequency = body.NewsDigestFrequency ?? EmailPreferences.NewsDigestFrequency,
        ModerationEmailsEnabled = body.ModerationEmailsEnabled ?? EmailPreferences.ModerationEmailsEnabled,
        CommunityDigestFrequency = body.CommunityDigestFrequency ?? EmailPreferences.CommunityDigestFrequency,
        ModerationEmailCadence = body.ModerationEmailCadence ?? EmailPreferences.ModerationEmailCadence,
        ModerationEmailDaysOfWeek = body.ModerationEmailDaysOfWeek ?? EmailPreferences.ModerationEmailDaysOfWeek,
        ModerationEmailTimeOfDay = body.ModerationEmailTimeOfDay ?? EmailPreferences.ModerationEmailTimeOfDay,
        ModerationEmailTimezone = body.ModerationEmailTimezone ?? EmailPreferences.ModerationEmailTimezone,
      };
      return Task.FromResult(new EmailPreferencesResponse(EmailPreferences));
    }

    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDataRequestResponse?>(CreateDataRequest());

    public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserDataRequestCreationResponse(
            "request-1",
            "ready",
            DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
            DateTimeOffset.Parse("2027-07-08T16:00:00-07:00")));

    public Task<DeleteUserResponse> DeleteUserAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default)
    {
      LastDeletedUserIdOrSlug = idOrSlug;
      return Task.FromResult(DeleteUserResponse);
    }

    public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApiKeyListResponse(
            [CreateApiKey()],
            InitialApiKeyPage));

    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
        string label,
        string type,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApiKeyCreationResponse(CreateApiKey(), "raw-key"));

    public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse([CreateProfileLink()], new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> CreateProfileLinkAsync(
        CreateProfileLinkBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkResponse(CreateProfileLink()));

    public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
        ReorderProfileLinksBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse([CreateProfileLink()], new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
        string id,
        UpdateProfileLinkBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkResponse(CreateProfileLink()));

    public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(MembershipResponse);

    public Task<MembershipPlansResponse> FetchMembershipPlansAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(MembershipPlansResponse);

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
        Task.FromResult(new WebPushSubscriptionListResponse([CreatePushSubscription()], InitialPushPage));

    public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static MyIdentityResponse CreateIdentity() =>
        new(new User(
            "user-1",
            "alice",
            Roles: ["member"],
            EmailAddress: "alice@example.com",
            MembershipPlan: "pro",
            DisplayNameSource: "github",
            ProfileImageId: "image-1"));

    private static MyProfileResponse CreateProfile() =>
        new(new MyProfile("profile-1", "Hello, Voucha!"));

    private static UserResponse CreateUser() =>
        new(new User(
            "user-1",
            "alice",
            "markdown",
            EmailAddress: "alice@example.com",
            UseDisplayNameFrom: "github",
            FollowsVisibility: "followers",
            CommunityMembershipsVisibility: "users",
            FollowersVisibility: "followers",
            LikesVisibility: "nobody",
            DirectMessagesAudience: "users",
            DefaultPostBroadcast: "followers",
            DefaultPostPrivacy: "private",
            UiLocale: "en",
            ProcessingRestrictedAt: DateTimeOffset.Parse("2026-07-01T22:00:00Z"),
            ThirdPartyMarketing: true));

    private static UserDataRequestResponse CreateDataRequest() =>
        new(
            Id: "request-1",
            UserId: "user-1",
            Status: "ready",
            CreatedAt: DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
            ExpiresAt: DateTimeOffset.Parse("2027-07-08T16:00:00-07:00"),
            DownloadUrl: new Uri("https://download"));

    public static ApiKey CreateApiKey() =>
        new(
            "api-key-1",
            "user-1",
            "rk_abc123",
            "rss",
            "Reader",
            ["rss-feeds:read"],
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

    public static Membership CreateMembership(string status = "active") =>
        new(
            "__entity_type",
            "membership-1",
            "user-1",
            "pro",
            status,
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            new MembershipSku("sku-1", "pro", new Money(1500, "usd"), "month", "price-1"));

    public static WebPushSubscription CreatePushSubscription() =>
        new(
            "__entity_type",
            "push-1",
            "user-1",
            "https://push.example.com",
            "p256dh",
            "auth",
            null,
            "Firefox",
            null,
            null,
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T12:00:00Z"));

    public static AuthSession CreateSession() =>
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
