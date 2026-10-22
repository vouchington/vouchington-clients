using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record NativeMembershipOverview(
    [property: JsonPropertyName("membership"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    NativeMembershipEntitlement? Membership,
    [property: JsonPropertyName("sources")] IReadOnlyList<NativeMembershipSource> Sources,
    [property: JsonPropertyName("pending")] NativeMembershipPending Pending,
    [property: JsonPropertyName("management"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    NativeMembershipManagement? Management)
{
  public string VisiblePlan => Membership?.Plan ?? "free";

  public bool BlocksStoreReplacement =>
      Membership is not null || Sources.Any(source => source.IsEffective || source.Status == "active");
}

public sealed record NativeMembershipEntitlement(
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("cancel_at_period_end")] bool? CancelAtPeriodEnd = null);

public sealed record NativeMembershipManagement(
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("provider")] string Provider);

public sealed record NativeMembershipPending(
    [property: JsonPropertyName("financial_operations")]
    IReadOnlyList<NativeMembershipFinancialOperation> FinancialOperations,
    [property: JsonPropertyName("grants")] int Grants,
    [property: JsonPropertyName("switches")] IReadOnlyList<NativeMembershipSwitch> Switches,
    [property: JsonPropertyName("verifications")]
    IReadOnlyList<NativeMembershipPendingVerification> Verifications);

public sealed record NativeMembershipFinancialOperation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("status")] string Status);

public sealed record NativeMembershipSwitch(
    [property: JsonPropertyName("eligible_at")] string EligibleAt,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("source_id")] string SourceId);

public sealed record NativeMembershipPendingVerification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("next_processing_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? NextProcessingAt,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("reason_code"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? ReasonCode,
    [property: JsonPropertyName("status")] string Status);

public sealed record NativeMembershipSource(
    [property: JsonPropertyName("access_ends_at")] string AccessEndsAt,
    [property: JsonPropertyName("effective_at")] string EffectiveAt,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("is_effective")] bool IsEffective,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("renewal"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    NativeMembershipRenewal? Renewal,
    [property: JsonPropertyName("should_auto_renew")] bool ShouldAutoRenew,
    [property: JsonPropertyName("status")] string Status);

public sealed record NativeMembershipRenewal(
    [property: JsonPropertyName("effective_at")] string EffectiveAt,
    [property: JsonPropertyName("price"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] Money? Price,
    [property: JsonPropertyName("product_id")] string ProductId);
