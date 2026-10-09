using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightEuDisputeSettlementsResponse(
    [property: JsonPropertyName("copyright_eu_dispute_settlements")] IReadOnlyList<CopyrightEuDisputeSettlement> CopyrightEuDisputeSettlements,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CopyrightEuParticipantCase(
    [property: JsonPropertyName("outcome"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Outcome,
    [property: JsonPropertyName("decided_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? DecidedAt,
    [property: JsonPropertyName("informed_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? InformedAt,
    [property: JsonPropertyName("reopened_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? ReopenedAt,
    [property: JsonPropertyName("complaint")] CopyrightEuComplaint Complaint,
    [property: JsonPropertyName("dispute_settlements")] IReadOnlyList<CopyrightEuDisputeSettlement> DisputeSettlements,
    [property: JsonPropertyName("dispute_settlements_page_info")] PageInfo DisputeSettlementsPageInfo);

public sealed record CopyrightEuComplaint(
    [property: JsonPropertyName("can_submit")] bool CanSubmit,
    [property: JsonPropertyName("window_ends_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? WindowEndsAt,
    [property: JsonPropertyName("request"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] CopyrightEuComplaintRequest? Request,
    [property: JsonPropertyName("decision"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] CopyrightEuComplaintDecision? Decision);

public sealed record CopyrightEuComplaintRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("received_at")] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("filed_by")] string FiledBy);

public sealed record CopyrightEuComplaintDecision(
    [property: JsonPropertyName("staff_disposition")] string StaffDisposition,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("decided_at")] DateTimeOffset DecidedAt);

public sealed record CopyrightEuDisputeSettlement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("body_name")] string BodyName,
    [property: JsonPropertyName("referred_at")] DateTimeOffset ReferredAt,
    [property: JsonPropertyName("outcome"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] CopyrightEuDisputeOutcome? Outcome);

public sealed record CopyrightEuDisputeOutcome(
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("decided_at")] DateTimeOffset DecidedAt,
    [property: JsonPropertyName("implemented_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? ImplementedAt);
