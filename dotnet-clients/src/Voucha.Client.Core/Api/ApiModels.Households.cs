using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum HouseholdAccess
{
  All,
  Owned,
  Member,
}

public sealed record Household(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("owner_id")] string OwnerId,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null);

public sealed record HouseholdIndividual(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record HouseholdMembership(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("household_id")] string HouseholdId,
    [property: JsonPropertyName("individual")] HouseholdIndividual Individual,
    [property: JsonPropertyName("relationship")] string? Relationship,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record HouseholdsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<Household> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record HouseholdMembershipsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<HouseholdMembership> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record HouseholdCreateResponse(
    [property: JsonPropertyName("household")] Household Household);
