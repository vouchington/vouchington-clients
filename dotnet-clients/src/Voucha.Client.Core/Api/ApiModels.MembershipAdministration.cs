using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum MembershipGrantPlanSlug { Plus, Pro }

public sealed record GrantMembershipBody(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("plan")] MembershipGrantPlanSlug Plan,
    [property: JsonPropertyName("sku_id")] string SkuId,
    [property: JsonPropertyName("duration_days")] int DurationDays);

public sealed record MembershipGrantResult([property: JsonPropertyName("id")] string Id);

public sealed record GrantMembershipResponse(
    [property: JsonPropertyName("grant")] MembershipGrantResult Grant,
    [property: JsonPropertyName("membership")] MembershipGrantResult Membership,
    [property: JsonPropertyName("queued")] bool Queued);
