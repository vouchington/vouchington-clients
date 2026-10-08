using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  private sealed partial class RecordingSettingsService
  {
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
    private UserResponse CreateUser() =>
        new(new User(
            "user-1",
            "alice",
            "markdown",
            FollowsVisibility: "followers",
            LikesVisibility: "nobody",
            DefaultPostPrivacy: "private",
            Roles: UserRoles,
            UiLocale: UserUiLocale,
            ProcessingRestrictedAt: DateTimeOffset.Parse("2026-07-01T22:00:00Z"),
            ThirdPartyMarketing: true));

    private static UserDataRequestResponse CreateDataRequest() =>
        new(
            Id: "request-1", UserId: "user-1",
            Status: "ready",
            CreatedAt: DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
            ExpiresAt: DateTimeOffset.Parse("2027-07-08T16:00:00-07:00"),
            DownloadUrl: new Uri("https://download"));

    private static ApiKey CreateApiKey() =>
        new(
            "api-key-1",
            "user-1",
            "rk_abc123",
            "rss",
            "Reader",
            ["rss:read"],
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

    private static Membership CreateMembership() =>
        new(
            EntityType: "__entity_type",
            Id: "membership-1",
            UserId: "user-1",
            Plan: "pro",
            Status: "active",
            StartedAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            ExpiresAt: null,
            StripeSubscriptionId: null,
            StripeCustomerId: null,
            HasStripeSubscription: true,
            GrantedById: null,
            CancelledAt: null,
            ExpiredAt: null,
            PastDueAt: null,
            PausedAt: null,
            CancelAtPeriodEnd: false,
            LatestChangeId: null,
            CreatedAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Sku: new MembershipSku("sku-1", "pro", new Money(1500, "usd"), "month", "price-1"));

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
}
