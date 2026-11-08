using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightAssociationTarget(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("claimant_display_name")] string ClaimantDisplayName,
    [property: JsonPropertyName("jurisdiction")] string Jurisdiction,
    [property: JsonPropertyName("received_at")] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("work_description")] string WorkDescription);

public sealed record CopyrightAssociationTargetsResponse(
    [property: JsonPropertyName("copyright_notices")] IReadOnlyList<CopyrightAssociationTarget> CopyrightNotices,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CopyrightRepeatInfringerAccount(
    [property: JsonPropertyName("account_user_id")] string AccountUserId,
    [property: JsonPropertyName("account_username")] string? AccountUsername,
    [property: JsonPropertyName("incident_id")] string IncidentId,
    [property: JsonPropertyName("is_operative")] bool IsOperative,
    [property: JsonPropertyName("open_review_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? OpenReviewId,
    [property: JsonPropertyName("termination_in_effect")] bool TerminationInEffect);

public sealed record CopyrightRepeatInfringerAccountsResponse(
    [property: JsonPropertyName("copyright_repeat_infringer_accounts")] IReadOnlyList<CopyrightRepeatInfringerAccount> Accounts);
