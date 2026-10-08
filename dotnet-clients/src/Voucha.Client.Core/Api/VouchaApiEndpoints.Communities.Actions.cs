namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest UpdateCommunityMemberRole(string idOrSlug, string userId, UpdateCommunityMemberRoleRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/members/{Path(userId)}") { Body = body };

  public static ApiRequest RemoveCommunityMember(string idOrSlug, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/members/{Path(userId)}");

  public static ApiRequest TransferCommunityOwnership(string idOrSlug, TransferCommunityOwnershipRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/ownership-transfers") { Body = body };

  public static ApiRequest RemoveCommunityListItem(string idOrSlug, string itemType, string itemId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/list-items/{Path(itemType)}/{Path(itemId)}");

  public static ApiRequest CommunityApplications(string idOrSlug, string? after = null, int limit = 20) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/applications", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityApplicationQuestions(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/application-questions");

  public static ApiRequest ReviewCommunityApplication(
      string idOrSlug,
      string applicationId,
      UpdateCommunityPostReviewRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/applications/{Path(applicationId)}") { Body = body };

  public static ApiRequest CommunityInvites(string idOrSlug, string? after = null, int limit = 20) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/invites", Query(("after", after), ("limit", limit)));

  public static ApiRequest SendCommunityInvite(string idOrSlug, SendCommunityInviteRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/invites") { Body = body };

  public static ApiRequest RevokeCommunityInvite(string idOrSlug, string inviteId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/invites/{Path(inviteId)}");

  public static ApiRequest RedeemCommunityInvite(RedeemCommunityInviteRequest body) =>
      new(HttpMethod.Post, "/api/v1/communities/invite-redemptions") { Body = body };

  public static ApiRequest ReviewCommunityPost(string idOrSlug, string postId, UpdateCommunityPostReviewRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}") { Body = body };

  public static ApiRequest UpdateCommunityPinnedPosts(string idOrSlug, CommunityPinnedPostsUpdateRequest body) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(idOrSlug)}/pinned-posts") { Body = body };

  public static ApiRequest CommunityBans(string idOrSlug, string? after = null, int? limit = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/bans", Query(("after", after), ("limit", limit)));

  public static ApiRequest BanCommunityMember(string idOrSlug, BanCommunityMemberRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/bans") { Body = body };

  public static ApiRequest LiftCommunityBan(string idOrSlug, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/bans/{Path(userId)}");

  public static ApiRequest CommunityRestrictions(string idOrSlug, string? after = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/restrictions", Query(("after", after)));

  public static ApiRequest ActivateCommunityRestrictions(string idOrSlug, ActivateCommunityRestrictionsRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/restrictions") { Body = body };

  public static ApiRequest LiftCommunityRestriction(string idOrSlug, string restrictionId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/restrictions/{Path(restrictionId)}");

  public static ApiRequest CommunityModlog(string idOrSlug, string? after = null, string? actionType = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/modlog", Query(("after", after), ("action_type", actionType)));

  public static ApiRequest CommunityModmail(string idOrSlug, string? after = null, int? limit = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/modmail", Query(("after", after), ("limit", limit)));

  public static ApiRequest OpenCommunityModmail(string idOrSlug, object body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/modmail") { Body = body };

  public static ApiRequest OpenCommunityModmailForReport(string idOrSlug, string reportId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/modmail") { Body = new { } };

  public static ApiRequest CommunityModmailMessages(string idOrSlug, string conversationId, string? after = null, int? limit = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/modmail/{Path(conversationId)}/messages", Query(("after", after), ("limit", limit)));

  public static ApiRequest SendCommunityModmailMessage(string idOrSlug, string conversationId, object body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/modmail/{Path(conversationId)}/messages") { Body = body };

  public static ApiRequest UpdateCommunityModmailThread(string idOrSlug, string conversationId, object body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/modmail/{Path(conversationId)}") { Body = body };

  public static ApiRequest CommunitySavedReplies(string idOrSlug, string? after = null, int? limit = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/saved-replies", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateCommunitySavedReply(string idOrSlug, object body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/saved-replies") { Body = body };

  public static ApiRequest DeleteCommunitySavedReply(string idOrSlug, string replyId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/saved-replies/{Path(replyId)}");

  public static ApiRequest CommunityModerationQueue(string idOrSlug, string? after = null, int? limit = null, string? source = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/moderation-queue", Query(("after", after), ("limit", limit), ("source", source)));

  public static ApiRequest DismissCommunityAutomodFlag(string idOrSlug, string postId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/automod-flag/dismissal");

  public static ApiRequest CommunityPendingReports(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      string sort = "created_at_desc") =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/reports/pending", Query(("limit", limit), ("sort", sort), ("after", after)));

  public static ApiRequest ClaimCommunityModerationReport(string idOrSlug, string reportId) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/claim") { Body = new { } };

  public static ApiRequest ReleaseCommunityModerationReport(string idOrSlug, string reportId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/claim");

  public static ApiRequest ClaimCommunityModerationPost(string idOrSlug, string postId) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/claim") { Body = new { } };

  public static ApiRequest ReleaseCommunityModerationPost(string idOrSlug, string postId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/claim");

  public static ApiRequest OpenCommunityModerationReportThread(string idOrSlug, string reportId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/mod-internal-thread") { Body = new { } };

  public static ApiRequest OpenCommunityModerationPostThread(string idOrSlug, string postId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/mod-internal-thread") { Body = new { } };

  public static ApiRequest EscalateCommunityModerationReport(string idOrSlug, string reportId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/escalation") { Body = new { } };

  public static ApiRequest DeescalateCommunityModerationReport(string idOrSlug, string reportId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}/escalation");

  public static ApiRequest EscalateCommunityModerationPost(string idOrSlug, string postId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/escalation") { Body = new { } };

  public static ApiRequest DeescalateCommunityModerationPost(string idOrSlug, string postId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/escalation");

  public static ApiRequest CommunityAiAgents(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/ai-agents");

  public static ApiRequest EnableCommunityAiAgent(string idOrSlug, string agentSlug) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(idOrSlug)}/ai-agents/{Path(agentSlug)}");

  public static ApiRequest DeleteCommunityAiAgent(string idOrSlug, string agentSlug) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/ai-agents/{Path(agentSlug)}");

  public static ApiRequest CommunityAgentPrompts(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/agent-prompts");

  public static ApiRequest CommunityAgentPrompt(string idOrSlug, string promptId) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}");

  public static ApiRequest CreateCommunityAgentPrompt(string idOrSlug, CommunityAgentPromptUpsertRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts") { Body = body };

  public static ApiRequest UpdateCommunityAgentPrompt(string idOrSlug, string promptId, CommunityAgentPromptUpdateRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}") { Body = body };

  public static ApiRequest DeleteCommunityAgentPrompt(string idOrSlug, string promptId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}");

  public static ApiRequest AllocateCommunityAgentPromptSlot(string idOrSlug, string promptId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}/allocations") { Body = new { } };

  public static ApiRequest DeallocateCommunityAgentPromptSlot(string idOrSlug, string promptId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}/allocations");

  public static ApiRequest TestCommunityAgentPrompt(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptTestRunRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/{Path(promptId)}/test-runs") { Body = body };

  public static ApiRequest CommunityAgentPromptHistory(string idOrSlug, string? promptId = null, string? before = null) =>
      Get(
          $"/api/v1/communities/{Path(idOrSlug)}/agent-prompts/history",
          Query(("promptId", promptId), ("before", before)));

  public static ApiRequest CommunityAutomodRecentActions(
      string idOrSlug,
      string? after = null,
      int? limit = 25,
      string? window = "24h",
      string? source = "agent_moderation") =>
      Get(
          $"/api/v1/communities/{Path(idOrSlug)}/automod/recent-actions",
          Query(("after", after), ("limit", limit), ("source", source), ("window", window)));

  public static ApiRequest RecordCommunityAutomodFeedback(string idOrSlug, string sourceKey, CommunityAutomodFeedbackRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/automod/recent-actions/{Path(sourceKey)}/feedback") { Body = body };

  public static ApiRequest SimulateCommunityAutomod(string idOrSlug, CommunityAutomodSimulationRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/automod/simulate") { Body = body };

  public static ApiRequest CommunityModerationAnalytics(string idOrSlug, string range = "30d") =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/moderation-analytics", Query(("range", range)));

  public static ApiRequest CommunityModerationTransparency(string idOrSlug, string range = "30d", string? after = null) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/moderation-transparency", Query(("range", range), ("after", after)));
}
