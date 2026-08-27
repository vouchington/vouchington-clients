using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CommunityModerationQueueEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("queue_source")] string QueueSource,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("report_count")] int ReportCount,
    [property: JsonPropertyName("action_at")] DateTimeOffset ActionAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("target_label")] string? TargetLabel,
    [property: JsonPropertyName("target_path")] string? TargetPath,
    [property: JsonPropertyName("target_available")] bool TargetAvailable,
    [property: JsonPropertyName("target_is_anonymous")] bool TargetIsAnonymous,
    [property: JsonPropertyName("target_is_restricted")] bool TargetIsRestricted,
    [property: JsonPropertyName("target_user_id")] string? TargetUserId,
    [property: JsonPropertyName("reporter_user_id")] string? ReporterUserId,
    [property: JsonPropertyName("reporter_username")] string? ReporterUsername,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("judgement")] JsonElement? Judgement,
    [property: JsonPropertyName("flagged_reason")] string? FlaggedReason,
    [property: JsonPropertyName("admin_action_path")] string? AdminActionPath,
    [property: JsonPropertyName("community_ban_evasion")] JsonElement? CommunityBanEvasion,
    [property: JsonPropertyName("post_moderation_context")] JsonElement? PostModerationContext,
    [property: JsonPropertyName("is_system_generated")] bool IsSystemGenerated,
    [property: JsonPropertyName("cursor_created_at")] DateTimeOffset? CursorCreatedAt,
    [property: JsonPropertyName("cursor_report_count")] int? CursorReportCount,
    [property: JsonPropertyName("cursor_severity_rank")] int? CursorSeverityRank,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt = null);

public sealed record CommunityModerationQueueResponse(
    [property: JsonPropertyName("entries")] IReadOnlyList<CommunityModerationQueueEntry> Entries,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("viewer_tier")] string ViewerTier);

public sealed record CommunityAiAgentsResponse(
    [property: JsonPropertyName("community_ai_agents")] IReadOnlyList<CommunityAiAgent> CommunityAiAgents);

public sealed record CommunityAgentPrompt(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("agent_id")] string AgentId,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("model_provider")] string ModelProvider,
    [property: JsonPropertyName("slot_allocated")] bool SlotAllocated,
    [property: JsonPropertyName("on_flag_action")] string OnFlagAction,
    [property: JsonPropertyName("activated_at")] DateTimeOffset? ActivatedAt = null,
    [property: JsonPropertyName("deactivated_at")] DateTimeOffset? DeactivatedAt = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt = default,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt = default);

public sealed record CommunityAgentPromptHistoryEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("agent_prompt_id")] string AgentPromptId,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("changed_by")] CommunityOwner? ChangedBy,
    [property: JsonPropertyName("previous_fields")] IReadOnlyDictionary<string, object> PreviousFields,
    [property: JsonPropertyName("next_fields")] IReadOnlyDictionary<string, object> NextFields,
    [property: JsonPropertyName("changed_fields")] IReadOnlyDictionary<string, object> ChangedFields,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record CommunityAgentPromptsResponse(
    [property: JsonPropertyName("community_agent_prompts")] IReadOnlyList<CommunityAgentPrompt> CommunityAgentPrompts,
    [property: JsonPropertyName("slot_info")] IReadOnlyDictionary<string, object> SlotInfo);

public sealed record CommunityAgentPromptResponse(
    [property: JsonPropertyName("community_agent_prompt")] CommunityAgentPrompt CommunityAgentPrompt);

public sealed record CommunityAgentPromptHistoryResponse(
    [property: JsonPropertyName("entries")] IReadOnlyList<CommunityAgentPromptHistoryEntry> Entries,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);

public sealed record CommunityAutomodSimulationResult(
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("post_type")] string PostType,
    [property: JsonPropertyName("approved_at")] DateTimeOffset ApprovedAt,
    [property: JsonPropertyName("content_excerpt")] string ContentExcerpt,
    [property: JsonPropertyName("flagged")] bool Flagged,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("would_unpublish")] bool WouldUnpublish);

public sealed record CommunityAutomodSimulation(
    [property: JsonPropertyName("simulation")] CommunityAutomodSimulationSummary Simulation,
    [property: JsonPropertyName("results")] IReadOnlyList<CommunityAutomodSimulationResult> Results);

public sealed record CommunityAutomodSimulationSummary(
    [property: JsonPropertyName("prompt_id")] string PromptId,
    [property: JsonPropertyName("time_window_hours")] int TimeWindowHours,
    [property: JsonPropertyName("sample_count")] int SampleCount,
    [property: JsonPropertyName("would_flag_count")] int WouldFlagCount,
    [property: JsonPropertyName("would_unpublish_count")] int WouldUnpublishCount,
    [property: JsonPropertyName("false_positive_estimate")] CommunityAutomodFalsePositiveEstimate? FalsePositiveEstimate);

public sealed record CommunityAutomodFalsePositiveEstimate(
    [property: JsonPropertyName("historical_flagged_count")] int HistoricalFlaggedCount,
    [property: JsonPropertyName("historical_approved_count")] int HistoricalApprovedCount,
    [property: JsonPropertyName("rate")] decimal? Rate);

public sealed record CommunityAutomodFeedbackResponse(
    [property: JsonPropertyName("applied_action")] bool AppliedAction,
    [property: JsonPropertyName("feedback")] JsonElement? Feedback = null);

public sealed record CommunityModerationAnalyticsScope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("community_id")] string CommunityId);

public sealed record CommunityModerationAnalyticsQueueVolume(
    [property: JsonPropertyName("total_reports")] int TotalReports,
    [property: JsonPropertyName("pending_reports")] int PendingReports,
    [property: JsonPropertyName("reports_over_time")] IReadOnlyList<object> ReportsOverTime,
    [property: JsonPropertyName("clearance_actions_over_time")] IReadOnlyList<object> ClearanceActionsOverTime,
    [property: JsonPropertyName("moderator_actions_over_time")] IReadOnlyList<object> ModeratorActionsOverTime);

public sealed record CommunityModerationAnalyticsAppeals(
    [property: JsonPropertyName("total_closed")] int TotalClosed,
    [property: JsonPropertyName("accepted")] int Accepted,
    [property: JsonPropertyName("reduced")] int Reduced,
    [property: JsonPropertyName("denied")] int Denied,
    [property: JsonPropertyName("dismissed")] int Dismissed,
    [property: JsonPropertyName("success_rate")] decimal? SuccessRate);

public sealed record CommunityModerationAnalyticsModeratorWorkload(
    [property: JsonPropertyName("moderators")] IReadOnlyList<object> Moderators,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User> Users);

public sealed record CommunityModerationAnalyticsAutomodPerformance(
    [property: JsonPropertyName("total_actions")] int TotalActions,
    [property: JsonPropertyName("auto_removes")] int AutoRemoves,
    [property: JsonPropertyName("reviewed_count")] int ReviewedCount,
    [property: JsonPropertyName("false_positive_count")] int FalsePositiveCount,
    [property: JsonPropertyName("false_positive_rate")] decimal? FalsePositiveRate,
    [property: JsonPropertyName("actions_over_time")] IReadOnlyList<object> ActionsOverTime,
    [property: JsonPropertyName("confidence_distribution")] IReadOnlyList<object> ConfidenceDistribution,
    [property: JsonPropertyName("sources")] IReadOnlyList<object> Sources);

public sealed record CommunityModerationAnalyticsNewUserFriction(
    [property: JsonPropertyName("first_posts")] int FirstPosts,
    [property: JsonPropertyName("rejected_first_posts")] int RejectedFirstPosts,
    [property: JsonPropertyName("rejection_rate")] decimal? RejectionRate);

public sealed record CommunityModerationAnalyticsResponse(
    [property: JsonPropertyName("scope")] CommunityModerationAnalyticsScope Scope,
    [property: JsonPropertyName("range")] string Range,
    [property: JsonPropertyName("period_start")] DateTimeOffset PeriodStart,
    [property: JsonPropertyName("period_end")] DateTimeOffset PeriodEnd,
    [property: JsonPropertyName("queue_volume")] CommunityModerationAnalyticsQueueVolume QueueVolume,
    [property: JsonPropertyName("rule_violations")] IReadOnlyDictionary<string, object> RuleViolations,
    [property: JsonPropertyName("appeals")] CommunityModerationAnalyticsAppeals Appeals,
    [property: JsonPropertyName("moderator_workload")] CommunityModerationAnalyticsModeratorWorkload ModeratorWorkload,
    [property: JsonPropertyName("automod_performance")] CommunityModerationAnalyticsAutomodPerformance AutomodPerformance,
    [property: JsonPropertyName("new_user_friction")] CommunityModerationAnalyticsNewUserFriction NewUserFriction);

#pragma warning restore CA1054, CA1056, CA1720
