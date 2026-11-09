using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CommunityBanDetailResponse(
    [property: JsonPropertyName("community_ban"), JsonRequired] CommunityBan CommunityBan);

public sealed record CommunityBanLiftResponse(
    [property: JsonPropertyName("community_id"), JsonRequired] string CommunityId,
    [property: JsonPropertyName("user_id"), JsonRequired] string UserId,
    [property: JsonPropertyName("lifted_ban_ids"), JsonRequired] IReadOnlyList<string> LiftedBanIds);

public sealed record CommunityBanLiftError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("cap"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Cap = null);
