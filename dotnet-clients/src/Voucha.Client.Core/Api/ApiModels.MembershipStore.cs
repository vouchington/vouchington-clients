using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record MembershipPurchaseIntentBody(
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("product_id")] string ProductId,
    [property: JsonPropertyName("provider")] string Provider);

public sealed record MembershipVerificationEvidence(
    [property: JsonPropertyName("signed_transaction")] string SignedTransaction);

public sealed record MembershipVerificationBody(
    [property: JsonPropertyName("evidence")] MembershipVerificationEvidence Evidence,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("purchase_intent_id")] string PurchaseIntentId);

public sealed record EmptyJsonObjectBody;

public sealed record MembershipRefundBody(
    [property: JsonPropertyName("cancel")] bool Cancel,
    [property: JsonPropertyName("charge_id")] string ChargeId,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("invoice_id")] string InvoiceId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("user_id")] string UserId);
