using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record NativeMembershipPurchaseIntentResponse(
    [property: JsonPropertyName("purchase_intent")] NativeMembershipPurchaseIntent PurchaseIntent);

public sealed record NativeMembershipPurchaseIntent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("launch")] JsonElement Launch,
    [property: JsonPropertyName("product_id")] string ProductId,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("replayed")] bool Replayed);

public sealed record NativeMembershipPurchaseConflict(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("eligible_at")] string EligibleAt,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("management")] NativeMembershipManagement Management);

public sealed record NativeMembershipVerificationResponse(
    [property: JsonPropertyName("verification")] NativeMembershipVerification Verification);

public sealed record NativeMembershipVerification(
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("reason_code"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? ReasonCode,
    [property: JsonPropertyName("status")] string Status);

public sealed record NativeMembershipIntentBody(
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("product_id")] string ProductId,
    [property: JsonPropertyName("provider")] string Provider);

public sealed record NativeMembershipVerificationSubmission(
    [property: JsonPropertyName("evidence")] JsonElement Evidence,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("purchase_intent_id")] string PurchaseIntentId);
