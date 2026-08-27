using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UserSearchResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("use_display_name_from")] string? UseDisplayNameFrom = null,
    [property: JsonPropertyName("is_official_account")] bool? IsOfficialAccount = null,
    [property: JsonPropertyName("verification_status")] string? VerificationStatus = null,
    [property: JsonPropertyName("verified_badge_visible")] bool? VerifiedBadgeVisible = null,
    [property: JsonPropertyName("verified_display_name")] string? VerifiedDisplayName = null,
    [property: JsonPropertyName("public_verified_name_display")] string? PublicVerifiedNameDisplay = null,
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles = null,
    [property: JsonPropertyName("display_account")] UserDisplayAccount? DisplayAccount = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("email_address")] string? EmailAddress = null,
    [property: JsonPropertyName("suspended_at")] DateTimeOffset? SuspendedAt = null,
    [property: JsonPropertyName("suspended_reason")] string? SuspendedReason = null,
    [property: JsonPropertyName("suspended_by_id")] string? SuspendedById = null);
