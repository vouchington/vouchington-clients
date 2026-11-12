using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record HouseholdInvitationUser(
    [property: JsonPropertyName("id"), JsonRequired] string Id,
    [property: JsonPropertyName("username"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Username);

public sealed record HouseholdInvitation(
    [property: JsonPropertyName("id"), JsonRequired] string Id,
    [property: JsonPropertyName("household_id"), JsonRequired] string HouseholdId,
    [property: JsonPropertyName("invited_user_id"), JsonRequired] string InvitedUserId,
    [property: JsonPropertyName("invited_by_id"), JsonRequired] string InvitedById,
    [property: JsonPropertyName("invited_user"), JsonRequired] HouseholdInvitationUser InvitedUser,
    [property: JsonPropertyName("invited_by"), JsonRequired] HouseholdInvitationUser InvitedBy,
    [property: JsonPropertyName("household_owner"), JsonRequired] HouseholdInvitationUser HouseholdOwner,
    [property: JsonPropertyName("relationship"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Relationship,
    [property: JsonPropertyName("accepted_at"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? AcceptedAt,
    [property: JsonPropertyName("declined_at"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? DeclinedAt,
    [property: JsonPropertyName("revoked_at"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("created_at"), JsonRequired] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at"), JsonRequired] DateTimeOffset UpdatedAt);

public sealed record HouseholdInvitationsResponse(
    [property: JsonPropertyName("results"), JsonRequired] IReadOnlyList<HouseholdInvitation> Results,
    [property: JsonPropertyName("page_info"), JsonRequired] PageInfo PageInfo);

public sealed record HouseholdInvitationResponse(
    [property: JsonPropertyName("invitation"), JsonRequired] HouseholdInvitation Invitation);

public sealed record HouseholdInvitationAcceptanceResponse(
    [property: JsonPropertyName("membership"), JsonRequired] HouseholdMembership Membership);
