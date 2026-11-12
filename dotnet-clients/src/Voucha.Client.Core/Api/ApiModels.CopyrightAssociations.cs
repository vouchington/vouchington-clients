using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightAssociationTarget(
    [property: JsonPropertyName("id"), JsonRequired] string Id,
    [property: JsonPropertyName("claimant_display_name"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ClaimantDisplayName,
    [property: JsonPropertyName("jurisdiction"), JsonRequired] string Jurisdiction,
    [property: JsonPropertyName("received_at"), JsonRequired] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("work_description"), JsonRequired] string WorkDescription);

public sealed record CopyrightAssociationTargetsResponse(
    [property: JsonPropertyName("copyright_notices"), JsonRequired] IReadOnlyList<CopyrightAssociationTarget> CopyrightNotices,
    [property: JsonPropertyName("page_info"), JsonRequired] PageInfo PageInfo);

public sealed record CopyrightRepeatInfringerAccount(
    [property: JsonPropertyName("account_user_id")] string AccountUserId,
    [property: JsonPropertyName("account_username"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AccountUsername,
    [property: JsonPropertyName("incident_id")] string IncidentId,
    [property: JsonPropertyName("is_operative")] bool IsOperative,
    [property: JsonPropertyName("open_review_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? OpenReviewId,
    [property: JsonPropertyName("termination_in_effect")] bool TerminationInEffect);

public sealed record CopyrightRepeatInfringerAccountsResponse(
    [property: JsonPropertyName("copyright_repeat_infringer_accounts")] IReadOnlyList<CopyrightRepeatInfringerAccount> Accounts);
