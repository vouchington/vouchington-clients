using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightStaffFormReview(
    [property: JsonPropertyName("intake_id")] string IntakeId,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("screening")] CopyrightStaffFormScreening? Screening,
    [property: JsonPropertyName("review")] CopyrightStaffIntakeReview? Review);

public sealed record CopyrightStaffFormScreening(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("recommendation")] string? Recommendation,
    [property: JsonPropertyName("rationale")] string? Rationale,
    [property: JsonPropertyName("guidance")] CopyrightStaffFormGuidance? Guidance);

public sealed record CopyrightStaffIntakeReview(
    [property: JsonPropertyName("is_accepted")] bool IsAccepted,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset ReviewedAt,
    [property: JsonPropertyName("reviewed_by_id")] string? ReviewedById);

public sealed record CopyrightStaffFormGuidance(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("elements")] IReadOnlyList<CopyrightStaffFormElement> Elements,
    [property: JsonPropertyName("risk_notes")] IReadOnlyList<CopyrightStaffFormRiskNote> RiskNotes,
    [property: JsonPropertyName("suggested_action")] string SuggestedAction);

public sealed record CopyrightStaffFormElement(
    [property: JsonPropertyName("element")] string Element,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("gap")] string? Gap);

public sealed record CopyrightStaffFormRiskNote(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("note")] string Note);
