using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056

public sealed record MembershipSku(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("price")] Money Price,
    [property: JsonPropertyName("interval")] string Interval,
    [property: JsonPropertyName("stripe_price_id")] string StripePriceId,
    [property: JsonPropertyName("retired_at")] DateTimeOffset? RetiredAt = null);

public sealed record Membership(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("stripe_subscription_id")] string? StripeSubscriptionId,
    [property: JsonPropertyName("stripe_customer_id")] string? StripeCustomerId,
    [property: JsonPropertyName("has_stripe_subscription")] bool? HasStripeSubscription,
    [property: JsonPropertyName("granted_by_id")] string? GrantedById,
    [property: JsonPropertyName("cancelled_at")] DateTimeOffset? CancelledAt,
    [property: JsonPropertyName("expired_at")] DateTimeOffset? ExpiredAt,
    [property: JsonPropertyName("past_due_at")] DateTimeOffset? PastDueAt,
    [property: JsonPropertyName("paused_at")] DateTimeOffset? PausedAt,
    [property: JsonPropertyName("cancel_at_period_end")] bool CancelAtPeriodEnd,
    [property: JsonPropertyName("latest_change_id")] string? LatestChangeId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("sku")] MembershipSku Sku);

public sealed record MembershipResponse([property: JsonPropertyName("membership")] Membership? Membership);

public sealed record MembershipPlansResponse(
    [property: JsonPropertyName("plans")] IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>> Plans,
    [property: JsonPropertyName("benefit_catalog")] MembershipBenefitCatalog? BenefitCatalog = null);

public sealed record MembershipBenefitCatalog(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("groups")] IReadOnlyList<MembershipBenefitGroup> Groups);

public sealed record MembershipBenefitGroup(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("benefits")] IReadOnlyList<MembershipBenefit> Benefits);

public sealed record MembershipBenefit(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("placements")] IReadOnlyList<string> Placements,
    [property: JsonPropertyName("values")] IReadOnlyDictionary<string, MembershipBenefitValue> Values);

public sealed record MembershipBenefitValue(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("access")] string? Access = null,
    [property: JsonPropertyName("included")] bool? Included = null,
    [property: JsonPropertyName("level")] string? Level = null,
    [property: JsonPropertyName("quantity")] int? Quantity = null);

public sealed record CheckoutSession(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url")] Uri Url);

public sealed record CheckoutSessionResponse(
    [property: JsonPropertyName("checkout_session")] CheckoutSession CheckoutSession);

public sealed record PortalSession([property: JsonPropertyName("url")] Uri Url);

public sealed record PortalSessionResponse(
    [property: JsonPropertyName("portal_session")] PortalSession PortalSession);

#pragma warning restore CA1054, CA1056
