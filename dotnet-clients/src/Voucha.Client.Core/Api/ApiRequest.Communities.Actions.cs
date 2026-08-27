using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record UpdateCommunityMemberRoleRequest(
    [property: JsonPropertyName("role")] string Role);

public sealed record UpdateCommunityPostReviewRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string? Reason = null);

public sealed record SendCommunityInviteRequest(
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("username")] string? Username = null);

public sealed record RedeemCommunityInviteRequest(
    [property: JsonPropertyName("code")] string Code);

public sealed record TransferCommunityOwnershipRequest(
    [property: JsonPropertyName("user_id")] string UserId);

public sealed record BanCommunityMemberRequest(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt = null);

public sealed record ActivateCommunityRestrictionsRequest(
    [property: JsonPropertyName("restriction_types")] IReadOnlyList<string> RestrictionTypes,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt = null,
    [property: JsonPropertyName("reason")] string? Reason = null);

public sealed record CommunityAutomodFeedbackRequest(
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason_code")] string? ReasonCode = null,
    [property: JsonPropertyName("note")] string? Note = null);

public sealed record CommunityPinnedPostsUpdateRequest(
    [property: JsonPropertyName("post_ids")] IReadOnlyList<string> PostIds);

public sealed record CommunityAgentPromptUpsertRequest(
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("model_name")] string? ModelName = null,
    [property: JsonPropertyName("model_provider")] string? ModelProvider = null);

public sealed record CommunityAgentPromptUpdateRequest(
    [property: JsonPropertyName("prompt")] string? Prompt = null);

public sealed record CommunityAgentPromptTestRunRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("save_for_training")] bool? SaveForTraining = null,
    [property: JsonPropertyName("expected_flagged")] bool? ExpectedFlagged = null,
    [property: JsonPropertyName("expected_reason")] string? ExpectedReason = null);

public sealed record CommunityAutomodSimulationRequest(
    [property: JsonPropertyName("prompt_id")] string PromptId,
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("time_window_hours")] int? TimeWindowHours = null,
    [property: JsonPropertyName("limit")] int? Limit = null);

#pragma warning restore CA1054, CA1056, CA1720
