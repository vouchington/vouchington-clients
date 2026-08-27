using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Communities;

internal sealed partial class ScriptedCommunitiesService
{
  public Queue<CommunityApplicationsResponse> ApplicationsResponses { get; } = [];

  public Queue<CommunityInvitesResponse> InvitesResponses { get; } = [];

  public Queue<CommunityBansResponse> BansResponses { get; } = [];

  public Queue<CommunityRestrictionsResponse> RestrictionsResponses { get; } = [];

  public Queue<CommunityModerationQueueResponse> ModerationQueueResponses { get; } = [];

  public Queue<ModeratorVacationResponse> ModeratorVacationResponses { get; } = [];

  public Queue<CommunityAiAgentsResponse> AiAgentsResponses { get; } = [];

  public Queue<CommunityAgentPromptsResponse> AgentPromptsResponses { get; } = [];

  public Queue<CommunityModeratorStatsResponse> ModeratorStatsResponses { get; } = [];

  public Queue<ModerationTransparencyResponse> ModerationTransparencyResponses { get; } = [];
  public Func<string, string, string?, CancellationToken, Task<ModerationTransparencyResponse>>? ModerationTransparencyHandler { get; set; }

  public List<string> MutationDetails { get; } = [];

  public int ModerationAnalyticsFetchCount { get; private set; }
  public string? ModerationAnalyticsRange { get; private set; }
  public string? ModerationTransparencyRange { get; private set; }
  public Exception? ModerationAnalyticsFailure { get; set; }
  public Func<string, string, CancellationToken, Task<CommunityModerationAnalyticsResponse>>? ModerationAnalyticsHandler { get; set; }

  public Task<CommunityApplicationsResponse> FetchApplicationsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(ApplicationsResponses.Count > 0 ? ApplicationsResponses.Dequeue() : Deserialize<CommunityApplicationsResponse>("""
          {
            "results": [{ "id": "application-1" }],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "community_applications": {
              "application-1": {
                "id": "application-1",
                "community_id": "community-1",
                "user_id": "applicant-1",
                "answers": {},
                "message": "Please approve",
                "reviewed_at": null,
                "reviewed_by_id": null,
                "approved_at": null,
                "rejected_at": null,
                "rejection_reason": null,
                "created_at": "2026-07-01T00:00:00Z"
              }
            }
          }
          """));

  public Task<CommunityApplicationQuestionsResponse> FetchApplicationQuestionsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityInvitesResponse> FetchInvitesAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(InvitesResponses.Count > 0 ? InvitesResponses.Dequeue() : Deserialize<CommunityInvitesResponse>("""
          {
            "results": [{ "id": "invite-1" }],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "community_invites": {
              "invite-1": {
                "id": "invite-1",
                "community_id": "community-1",
                "code": "code-1",
                "invited_user_id": null,
                "invited_email": "person@example.com",
                "invited_by_id": "user-1",
                "accepted_at": null,
                "accepted_by_user_id": null,
                "declined_at": null,
                "revoked_at": null,
                "created_at": "2026-07-01T00:00:00Z"
              }
            }
          }
          """));

  public Task<CommunityBansResponse> FetchBansAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(BansResponses.Count > 0 ? BansResponses.Dequeue() : Deserialize<CommunityBansResponse>("""
          {
            "results": [{ "id": "ban-1" }],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "community_bans": {
              "ban-1": {
                "id": "ban-1",
                "case_id": "case-1",
                "community_id": "community-1",
                "user_id": "banned-user",
                "banned_by_id": "user-1",
                "reason": "spam",
                "expires_at": null,
                "created_at": "2026-07-01T00:00:00Z",
                "updated_at": "2026-07-01T00:00:00Z",
                "lifted_at": null,
                "lifted_by_id": null
              }
            },
            "users": {}
          }
          """));

  public Task<CommunityRestrictionsResponse> FetchRestrictionsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(RestrictionsResponses.Count > 0 ? RestrictionsResponses.Dequeue() : Deserialize<CommunityRestrictionsResponse>("""
          {
            "results": [{ "id": "restriction-1" }],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "community_restrictions": {
              "restriction-1": {
                "id": "restriction-1",
                "community_id": "community-1",
                "restriction_type": "raid_mode",
                "activated_by_id": "user-1",
                "activated_at": "2026-07-01T00:00:00Z",
                "expires_at": null,
                "created_at": "2026-07-01T00:00:00Z",
                "updated_at": "2026-07-01T00:00:00Z",
                "lifted_at": null,
                "lifted_by_id": null,
                "reason": "spike"
              }
            },
            "raid_mode_suggestion": null
          }
          """));

  public Task<ModlogResponse> FetchModlogAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      ModlogResponses.Count > 0
          ? Task.FromResult(ModlogResponses.Dequeue())
          : throw new NotSupportedException();

  public Task<ModeratorVacationResponse> FetchModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(ModeratorVacationResponses.Count > 0 ? ModeratorVacationResponses.Dequeue() : Deserialize<ModeratorVacationResponse>("""
          {
            "vacation": {
              "community_id": "community-1",
              "user_id": "user-1",
              "starts_at": "2026-07-01T00:00:00Z",
              "ends_at": "2026-07-08T00:00:00Z",
              "created_at": "2026-07-01T00:00:00Z",
              "updated_at": "2026-07-01T00:00:00Z"
            }
          }
          """));

  public Task<CommunityModeratorStatsResponse> FetchModeratorStatsAsync(
      string idOrSlug,
      int window = 30,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(ModeratorStatsResponses.Count > 0 ? ModeratorStatsResponses.Dequeue() : Deserialize<CommunityModeratorStatsResponse>("""
          {
            "window": 30,
            "stats": [{ "actor_id": "user-1", "total": 2, "counts": { "approve": 2 } }],
            "users": {}
          }
          """));

  public Task<CommunityModerationQueueResponse> FetchModerationQueueAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(ModerationQueueResponses.Count > 0 ? ModerationQueueResponses.Dequeue() : Deserialize<CommunityModerationQueueResponse>("""
          {
            "entries": [
              {
                "id": "queue-1",
                "community_id": "community-1",
                "entity_type": "post",
                "entity_id": "post-1",
                "post_id": "post-1",
                "status": "pending",
                "queue_source": "report",
                "reason": null,
                "note": null,
                "report_count": 1,
                "action_at": "2026-07-01T00:00:00Z",
                "created_at": "2026-07-01T00:00:00Z",
                "target_label": null,
                "target_path": null,
                "target_available": true,
                "target_is_anonymous": false,
                "target_is_restricted": false,
                "target_user_id": null,
                "reporter_user_id": null,
                "reporter_username": null,
                "resolved_by_id": null,
                "judgement": null,
                "flagged_reason": "reported",
                "admin_action_path": null,
                "community_ban_evasion": null,
                "post_moderation_context": null,
                "is_system_generated": false,
                "cursor_created_at": null,
                "cursor_report_count": null,
                "cursor_severity_rank": null
              }
            ],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "viewer_tier": "moderator"
          }
          """));

  public Task<CommunityModerationAnalyticsResponse> FetchModerationAnalyticsAsync(
      string idOrSlug,
      string range = "30d",
      CancellationToken cancellationToken = default)
  {
    ModerationAnalyticsFetchCount++;
    ModerationAnalyticsRange = range;
    if (ModerationAnalyticsHandler is not null)
    {
      return ModerationAnalyticsHandler(idOrSlug, range, cancellationToken);
    }
    return ModerationAnalyticsFailure is not null
        ? Task.FromException<CommunityModerationAnalyticsResponse>(ModerationAnalyticsFailure)
        : Task.FromResult(Deserialize<CommunityModerationAnalyticsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.moderation-analytics.default")));
  }

  public Task<ModerationTransparencyResponse> FetchModerationTransparencyAsync(
      string idOrSlug,
      string range = "30d",
      string? after = null,
      CancellationToken cancellationToken = default)
  {
    ModerationTransparencyRange = range;
    if (ModerationTransparencyHandler is not null)
    {
      return ModerationTransparencyHandler(idOrSlug, range, after, cancellationToken);
    }
    return ModerationTransparencyFailure is not null
        ? Task.FromException<ModerationTransparencyResponse>(ModerationTransparencyFailure)
        : Task.FromResult(ModerationTransparencyResponses.Count > 0
            ? ModerationTransparencyResponses.Dequeue()
            : new ModerationTransparencyResponse(range, []));
  }

  public Task<CommunityAiAgentsResponse> FetchAiAgentsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(AiAgentsResponses.Count > 0 ? AiAgentsResponses.Dequeue() : Deserialize<CommunityAiAgentsResponse>("""
          {
            "community_ai_agents": [
              {
                "agent_id": "agent-1",
                "slug": "spam-filter",
                "name": "Spam Filter",
                "description": "Filters spam",
                "enabled": true,
                "on_flag_action": "queue",
                "required_membership_plan": null
              }
            ]
          }
          """));

  public Task<CommunityAgentPromptsResponse> FetchAgentPromptsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(AgentPromptsResponses.Count > 0 ? AgentPromptsResponses.Dequeue() : Deserialize<CommunityAgentPromptsResponse>("""
          {
            "community_agent_prompts": [
              {
                "id": "prompt-1",
                "community_id": "community-1",
                "created_by_id": "user-1",
                "agent_id": "agent-1",
                "prompt": "Flag spam",
                "model_name": "model",
                "model_provider": "provider",
                "slot_allocated": true,
                "on_flag_action": "queue",
                "activated_at": null,
                "deactivated_at": null,
                "deleted_at": null,
                "deleted_by_id": null,
                "created_at": "2026-07-01T00:00:00Z",
                "updated_at": "2026-07-01T00:00:00Z"
              }
            ],
            "slot_info": {}
          }
          """));

  public Task<CommunityAgentPromptHistoryResponse> FetchAgentPromptHistoryAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityAutomodSimulation> SimulateAutomodAsync(
      string idOrSlug,
      CommunityAutomodSimulationRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityAutomodFeedbackResponse> RecordAutomodFeedbackAsync(
      string idOrSlug,
      string sourceKey,
      CommunityAutomodFeedbackRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task UpdateMemberRoleAsync(string idOrSlug, string userId, string role, CancellationToken cancellationToken = default) =>
      RecordCompleted("member-role", idOrSlug, $"{userId}:{role}");

  public Task RemoveMemberAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      RecordCompleted("member-remove", idOrSlug, userId);

  public Task TransferOwnershipAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      RecordCompleted("transfer", idOrSlug, userId);

  public Task AddListItemAsync(string idOrSlug, CommunityListItemRequest request, CancellationToken cancellationToken = default) =>
      RecordCompleted("list-add", idOrSlug, request.TopicId ?? request.RssFeedId ?? request.PostId ?? request.UrlHostnameId ?? request.UrlId ?? "");

  public Task RemoveListItemAsync(string idOrSlug, string itemType, string itemId, CancellationToken cancellationToken = default) =>
      RecordCompleted("list-remove", idOrSlug, $"{itemType}:{itemId}");

  public Task ReviewApplicationAsync(
      string idOrSlug,
      string applicationId,
      UpdateCommunityPostReviewRequest request,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("application-review", idOrSlug, $"{applicationId}:{request.Status}:{request.Reason}");

  public Task RevokeInviteAsync(string idOrSlug, string inviteId, CancellationToken cancellationToken = default) =>
      RecordCompleted("invite-revoke", idOrSlug, inviteId);

  public Task ReviewPostAsync(
      string idOrSlug,
      string postId,
      UpdateCommunityPostReviewRequest request,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("post-review", idOrSlug, $"{postId}:{request.Status}:{request.Reason}");

  public Task UpdatePinnedPostsAsync(
      string idOrSlug,
      CommunityPinnedPostsUpdateRequest request,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("pinned-posts", idOrSlug, string.Join(",", request.PostIds));

  public Task BanAsync(string idOrSlug, BanCommunityMemberRequest request, CancellationToken cancellationToken = default) =>
      RecordCompleted("ban", idOrSlug, $"{request.UserId}:{request.Reason}");

  public Task LiftBanAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      RecordCompleted("ban-lift", idOrSlug, userId);

  public Task ActivateRestrictionsAsync(
      string idOrSlug,
      ActivateCommunityRestrictionsRequest request,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("restrictions-activate", idOrSlug, string.Join(",", request.RestrictionTypes));

  public Task LiftRestrictionAsync(string idOrSlug, string restrictionId, CancellationToken cancellationToken = default) =>
      RecordCompleted("restriction-lift", idOrSlug, restrictionId);

  public Task SetModeratorVacationAsync(
      string idOrSlug,
      DateTimeOffset? endsAt = null,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("vacation-set", idOrSlug, endsAt?.ToString("O") ?? "");

  public Task ClearModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      RecordCompleted("vacation-clear", idOrSlug, "");

  public Task SetSuppressCommunityDigestsWhileOnVacationAsync(
      string idOrSlug,
      bool suppress,
      CancellationToken cancellationToken = default) =>
      RecordCompleted("vacation-digest-suppression", idOrSlug, suppress.ToString());

  public Task ClaimModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      RecordCompleted("report-claim", idOrSlug, reportId);

  public Task ReleaseModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      RecordCompleted("report-release", idOrSlug, reportId);

  public Task ClaimModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task ReleaseModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task OpenModerationReportThreadAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task OpenModerationPostThreadAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task EscalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task DeescalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task EscalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task DeescalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  private Task RecordCompleted(string method, string idOrSlug, string detail)
  {
    MutationCalls.Add((method, idOrSlug));
    MutationDetails.Add(detail);
    return Task.CompletedTask;
  }

  private static T Deserialize<T>(string json) =>
      JsonSerializer.Deserialize<T>(json, VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"{typeof(T).Name} JSON did not deserialize.");
}
