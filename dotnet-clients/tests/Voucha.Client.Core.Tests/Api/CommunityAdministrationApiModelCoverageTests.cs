using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CommunityAdministrationApiModelCoverageTests
{
  [Fact]
  public void VacationDigestPreferenceEndpointAndResponseUseIndependentBoolean()
  {
    var request = VouchaApiEndpoints.SetSuppressCommunityDigestsWhileOnVacation("test community", true);
    var json = JsonSerializer.Serialize(request.Body, VouchaApiJson.Options);
    var response = JsonSerializer.Deserialize<ModeratorVacationDigestPreferenceResponse>(json, VouchaApiJson.Options);

    Assert.Equal(HttpMethod.Patch, request.Method);
    Assert.Equal("/api/v1/communities/test%20community/moderator-vacation", request.Path);
    Assert.Equal("{\"suppress_community_digests_while_on_vacation\":true}", json);
    Assert.True(response!.SuppressCommunityDigestsWhileOnVacation);
  }

  [Fact]
  public void CommunityManagementRecordsExposeAllResponseShapes()
  {
    var pageInfo = new PageInfo("cursor-end", true, "cursor-start");
    var reference = new EntityReference("community_application", "application-1", "application-1", null, null, null, null, null, null, null, null);
    var createdAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var updatedAt = DateTimeOffset.Parse("2026-07-02T00:00:00Z");
    var question = new CommunityApplicationQuestion(
        "question-1",
        "community-1",
        "Why join?",
        "select",
        ["Because"],
        1,
        true,
        createdAt,
        updatedAt);
    var application = new CommunityApplication(
        "application-1",
        "community-1",
        "user-1",
        new Dictionary<string, object> { ["question-1"] = "Because" },
        "Please let me in",
        createdAt,
        "moderator-1",
        createdAt,
        null,
        null,
        createdAt,
        "community_application");
    var invite = new CommunityInvite(
        "invite-1",
        "community-1",
        "CODE",
        "user-2",
        "test@example.com",
        "moderator-1",
        createdAt,
        "user-2",
        null,
        null,
        createdAt,
        "community_invite");
    var ban = new CommunityBan(
        "ban-1",
        "case-1",
        "community-1",
        "user-3",
        "moderator-1",
        "spam",
        createdAt.AddDays(1),
        createdAt,
        updatedAt,
        null,
        null,
        "community_ban");
    var restriction = new CommunityRestriction(
        "restriction-1",
        "community-1",
        "invite_only",
        "moderator-1",
        createdAt,
        createdAt.AddHours(6),
        createdAt,
        updatedAt,
        null,
        null,
        "raid",
        "community_restriction");
    var suggestion = new RaidModeSuggestion(true, 4, createdAt);
    var modlogAction = new ModeratorActionView(
        Id: "action-1",
        CommunityId: "community-1",
        ActorId: "moderator-1",
        ActionType: "ban",
        PostId: "post-1",
        TargetUserId: "user-3",
        ReportId: "report-1",
        ReviewDisputeId: "dispute-1",
        CommunityApplicationId: "application-1",
        Reason: "spam",
        Metadata: new Dictionary<string, object> { ["severity"] = "high" },
        CreatedAt: createdAt,
        ReportedUserId: null);
    var vacation = new CommunityMemberVacation("community-1", "moderator-1", createdAt, updatedAt, createdAt, updatedAt);

    var applications = new CommunityApplicationsResponse([reference], pageInfo, new Dictionary<string, CommunityApplication> { [application.Id] = application });
    var questions = new CommunityApplicationQuestionsResponse([question]);
    var invites = new CommunityInvitesResponse([reference], pageInfo, new Dictionary<string, CommunityInvite> { [invite.Id] = invite });
    var bans = new CommunityBansResponse([reference], pageInfo, new Dictionary<string, CommunityBan> { [ban.Id] = ban }, new Dictionary<string, PublicUser>());
    var restrictions = new CommunityRestrictionsResponse(
        [reference],
        pageInfo,
        new Dictionary<string, CommunityRestriction> { [restriction.Id] = restriction },
        suggestion);
    var activated = new ActivateCommunityRestrictionsResponse(new Dictionary<string, CommunityRestriction> { [restriction.Id] = restriction });
    var modlog = new ModlogResponse([reference], pageInfo, new Dictionary<string, ModeratorActionView> { [modlogAction.Id] = modlogAction }, new Dictionary<string, PublicUser>());
    var moderatorVacation = new ModeratorVacationResponse(vacation);

    Assert.Equal("question-1", questions.Questions[0].Id);
    Assert.Equal("community-1", questions.Questions[0].CommunityId);
    Assert.Equal("Why join?", questions.Questions[0].Question);
    Assert.Equal("select", questions.Questions[0].FieldType);
    Assert.Equal("Because", questions.Questions[0].Options![0]);
    Assert.Equal(1, questions.Questions[0].OrderIndex);
    Assert.True(questions.Questions[0].Required);
    Assert.Equal(createdAt, questions.Questions[0].CreatedAt);
    Assert.Equal("application-1", applications.CommunityApplications["application-1"].Id);
    Assert.Equal("community-1", applications.CommunityApplications["application-1"].CommunityId);
    Assert.Equal("user-1", applications.CommunityApplications["application-1"].UserId);
    Assert.Equal("Because", applications.CommunityApplications["application-1"].Answers["question-1"]);
    Assert.Equal("Please let me in", applications.CommunityApplications["application-1"].Message);
    Assert.Equal(createdAt, applications.CommunityApplications["application-1"].ReviewedAt);
    Assert.Equal("moderator-1", applications.CommunityApplications["application-1"].ReviewedById);
    Assert.Equal(createdAt, applications.CommunityApplications["application-1"].ApprovedAt);
    Assert.Equal(createdAt, applications.CommunityApplications["application-1"].CreatedAt);
    Assert.Equal("community_application", applications.CommunityApplications["application-1"].EntityType);
    Assert.Equal("invite-1", invites.CommunityInvites["invite-1"].Id);
    Assert.Equal("community-1", invites.CommunityInvites["invite-1"].CommunityId);
    Assert.Equal("CODE", invites.CommunityInvites["invite-1"].Code);
    Assert.Equal("user-2", invites.CommunityInvites["invite-1"].InvitedUserId);
    Assert.Equal("test@example.com", invites.CommunityInvites["invite-1"].InvitedEmail);
    Assert.Equal("moderator-1", invites.CommunityInvites["invite-1"].InvitedById);
    Assert.Equal(createdAt, invites.CommunityInvites["invite-1"].AcceptedAt);
    Assert.Equal("user-2", invites.CommunityInvites["invite-1"].AcceptedByUserId);
    Assert.Equal(createdAt, invites.CommunityInvites["invite-1"].CreatedAt);
    Assert.Equal("community_invite", invites.CommunityInvites["invite-1"].EntityType);
    Assert.Equal("ban-1", bans.CommunityBans["ban-1"].Id);
    Assert.Equal("case-1", bans.CommunityBans["ban-1"].CaseId);
    Assert.Equal("community-1", bans.CommunityBans["ban-1"].CommunityId);
    Assert.Equal("user-3", bans.CommunityBans["ban-1"].UserId);
    Assert.Equal("moderator-1", bans.CommunityBans["ban-1"].BannedById);
    Assert.Equal("spam", bans.CommunityBans["ban-1"].Reason);
    Assert.Equal(createdAt.AddDays(1), bans.CommunityBans["ban-1"].ExpiresAt);
    Assert.Equal(createdAt, bans.CommunityBans["ban-1"].CreatedAt);
    Assert.Equal(updatedAt, bans.CommunityBans["ban-1"].UpdatedAt);
    Assert.Equal("community_ban", bans.CommunityBans["ban-1"].EntityType);
    Assert.Equal("restriction-1", restrictions.CommunityRestrictions["restriction-1"].Id);
    Assert.Equal("community-1", restrictions.CommunityRestrictions["restriction-1"].CommunityId);
    Assert.Equal("moderator-1", restrictions.CommunityRestrictions["restriction-1"].ActivatedById);
    Assert.Equal(createdAt, restrictions.CommunityRestrictions["restriction-1"].ActivatedAt);
    Assert.Equal(createdAt.AddHours(6), restrictions.CommunityRestrictions["restriction-1"].ExpiresAt);
    Assert.Equal(createdAt, restrictions.CommunityRestrictions["restriction-1"].CreatedAt);
    Assert.Equal(updatedAt, restrictions.CommunityRestrictions["restriction-1"].UpdatedAt);
    Assert.Equal("raid", restrictions.CommunityRestrictions["restriction-1"].Reason);
    Assert.Equal("community_restriction", restrictions.CommunityRestrictions["restriction-1"].EntityType);
    Assert.True(restrictions.RaidModeSuggestion!.VelocitySpike);
    Assert.Equal(4, restrictions.RaidModeSuggestion.FlagCount);
    Assert.Equal(createdAt, restrictions.RaidModeSuggestion.LatestFlaggedAt);
    Assert.Equal("invite_only", activated.CommunityRestrictions["restriction-1"].RestrictionType);
    Assert.Equal("action-1", modlog.ModeratorActions["action-1"].Id);
    Assert.Equal("community-1", modlog.ModeratorActions["action-1"].CommunityId);
    Assert.Equal("moderator-1", modlog.ModeratorActions["action-1"].ActorId);
    Assert.Equal("ban", modlog.ModeratorActions["action-1"].ActionType);
    Assert.Equal("post-1", modlog.ModeratorActions["action-1"].PostId);
    Assert.Equal("user-3", modlog.ModeratorActions["action-1"].TargetUserId);
    Assert.Equal("report-1", modlog.ModeratorActions["action-1"].ReportId);
    Assert.Equal("dispute-1", modlog.ModeratorActions["action-1"].ReviewDisputeId);
    Assert.Equal("application-1", modlog.ModeratorActions["action-1"].CommunityApplicationId);
    Assert.Equal("spam", modlog.ModeratorActions["action-1"].Reason);
    Assert.Equal("high", modlog.ModeratorActions["action-1"].Metadata["severity"]);
    Assert.Equal(createdAt, modlog.ModeratorActions["action-1"].CreatedAt);
    Assert.Equal("community-1", moderatorVacation.Vacation!.CommunityId);
    Assert.Equal("moderator-1", moderatorVacation.Vacation.UserId);
    Assert.Equal(createdAt, moderatorVacation.Vacation.StartsAt);
    Assert.Equal(updatedAt, moderatorVacation.Vacation.EndsAt);
    Assert.Equal(createdAt, moderatorVacation.Vacation.CreatedAt);
    Assert.Equal(updatedAt, moderatorVacation.Vacation.UpdatedAt);
    Assert.Equal("cursor-end", applications.PageInfo.EndCursor);
  }

  [Fact]
  public void CommunityModerationAndModmailRecordsExposeAllResponseShapes()
  {
    var pageInfo = new PageInfo(null, false, null);
    var createdAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var updatedAt = DateTimeOffset.Parse("2026-07-02T00:00:00Z");
    using var judgementDocument = JsonDocument.Parse("\"remove\"");
    using var banEvasionDocument = JsonDocument.Parse("""{"community_slug":"community-1","score":0.42}""");
    using var moderationContextDocument = JsonDocument.Parse("""
        {"openai_moderation":{"flagged":true},"agent_added_tags":["topic-1"]}
        """);
    var queueEntry = new CommunityModerationQueueEntry(
        "queue-1",
        "community-1",
        "post",
        "post-1",
        "post-1",
        "pending",
        "reports",
        "spam",
        "needs review",
        3,
        createdAt,
        createdAt,
        "Post title",
        "/posts/post-1",
        true,
        false,
        true,
        "user-1",
        "reporter-1",
        "reporter",
        "moderator-1",
        judgementDocument.RootElement.Clone(),
        "spam",
        "/admin/reports/report-1",
        banEvasionDocument.RootElement.Clone(),
        moderationContextDocument.RootElement.Clone(),
        false,
        createdAt,
        3,
        2);
    var prompt = new CommunityAgentPrompt(
        "prompt-1",
        "community-1",
        "moderator-1",
        "agent-1",
        "Prompt",
        "gpt-test",
        "openai",
        true,
        "queue",
        ActivatedAt: createdAt,
        CreatedAt: createdAt,
        UpdatedAt: updatedAt);
    var history = new CommunityAgentPromptHistoryEntry(
        "history-1",
        "prompt-1",
        "community-1",
        "update",
        new CommunityOwner("moderator-1", "mod"),
        new Dictionary<string, object> { ["prompt"] = "old" },
        new Dictionary<string, object> { ["prompt"] = "new" },
        new Dictionary<string, object> { ["prompt"] = true },
        createdAt);
    var simulationResult = new CommunityAutomodSimulationResult("post-1", "Title", "review", createdAt, "Excerpt", true, "spam", true);
    var falsePositiveEstimate = new CommunityAutomodFalsePositiveEstimate(10, 2, 0.2m);
    var simulationSummary = new CommunityAutomodSimulationSummary("prompt-1", 24, 5, 1, 1, falsePositiveEstimate);
    var analytics = new CommunityModerationAnalyticsResponse(
        new CommunityModerationAnalyticsScope("community", "community-1"),
        "30d",
        createdAt.AddDays(-30),
        createdAt,
        new CommunityModerationAnalyticsQueueVolume(5, 2, ["reports"], ["clearance"], ["moderator"]),
        new Dictionary<string, object> { ["spam"] = 3 },
        new CommunityModerationAnalyticsAppeals(4, 1, 1, 1, 1, 0.5m),
        new CommunityModerationAnalyticsModeratorWorkload(
            ["moderator"],
            new Dictionary<string, User> { ["moderator-1"] = new User("moderator-1", "mod", null) }),
        new CommunityModerationAnalyticsAutomodPerformance(6, 3, 2, 1, 0.5m, ["actions"], ["confidence"], ["sources"]),
        new CommunityModerationAnalyticsNewUserFriction(8, 2, 0.25m));
    var thread = new CommunityModmailThread(
        "thread-1",
        "modmail",
        "",
        "community-1",
        "user-1",
        "moderator-1",
        updatedAt,
        null,
        null,
        "user-1",
        createdAt,
        updatedAt);
    var message = new CommunityModmailMessage("message-1", "thread-1", "Hello", "moderator-1", "mod", createdAt);
    var savedReply = new CommunitySavedReply("reply-1", "community-1", "Greeting", "Hello", createdAt);

    var queue = new CommunityModerationQueueResponse([queueEntry], pageInfo, "moderator");
    var prompts = new CommunityAgentPromptsResponse([prompt], new Dictionary<string, object> { ["available"] = 1 });
    var historyResponse = new CommunityAgentPromptHistoryResponse([history], "cursor-1");
    var simulation = new CommunityAutomodSimulation(simulationSummary, [simulationResult]);
    var threads = new CommunityModmailThreadListResponse([thread], pageInfo);
    var messages = new CommunityModmailMessageListResponse([message], pageInfo);
    var savedReplies = new CommunitySavedRepliesResponse([savedReply], pageInfo);
    var savedReplyResponse = new CommunitySavedReplyResponse(savedReply);

    Assert.Equal("moderator", queue.ViewerTier);
    Assert.Equal("queue-1", queue.Entries[0].Id);
    Assert.Equal("community-1", queue.Entries[0].CommunityId);
    Assert.Equal("post", queue.Entries[0].EntityType);
    Assert.Equal("post-1", queue.Entries[0].EntityId);
    Assert.Equal("post-1", queue.Entries[0].PostId);
    Assert.Equal("pending", queue.Entries[0].Status);
    Assert.Equal("reports", queue.Entries[0].QueueSource);
    Assert.Equal("spam", queue.Entries[0].Reason);
    Assert.Equal("needs review", queue.Entries[0].Note);
    Assert.Equal(3, queue.Entries[0].ReportCount);
    Assert.Equal(createdAt, queue.Entries[0].ActionAt);
    Assert.Equal(createdAt, queue.Entries[0].CreatedAt);
    Assert.Equal("Post title", queue.Entries[0].TargetLabel);
    Assert.Equal("/posts/post-1", queue.Entries[0].TargetPath);
    Assert.True(queue.Entries[0].TargetAvailable);
    Assert.False(queue.Entries[0].TargetIsAnonymous);
    Assert.True(queue.Entries[0].TargetIsRestricted);
    Assert.Equal("user-1", queue.Entries[0].TargetUserId);
    Assert.Equal("reporter-1", queue.Entries[0].ReporterUserId);
    Assert.Equal("reporter", queue.Entries[0].ReporterUsername);
    Assert.Equal("moderator-1", queue.Entries[0].ResolvedById);
    Assert.Equal("remove", queue.Entries[0].Judgement!.Value.GetString());
    Assert.Equal("spam", queue.Entries[0].FlaggedReason);
    Assert.Equal("/admin/reports/report-1", queue.Entries[0].AdminActionPath);
    var banEvasion = Assert.NotNull(queue.Entries[0].CommunityBanEvasion);
    var postModerationContext = Assert.NotNull(queue.Entries[0].PostModerationContext);
    Assert.Equal(JsonValueKind.Object, banEvasion.ValueKind);
    Assert.Equal(0.42, banEvasion.GetProperty("score").GetDouble());
    Assert.Equal(JsonValueKind.Object, postModerationContext.ValueKind);
    Assert.True(
        postModerationContext.GetProperty("openai_moderation").GetProperty("flagged").GetBoolean());
    Assert.False(queue.Entries[0].IsSystemGenerated);
    Assert.Equal(createdAt, queue.Entries[0].CursorCreatedAt);
    Assert.Equal(3, queue.Entries[0].CursorReportCount);
    Assert.Equal(2, queue.Entries[0].CursorSeverityRank);
    Assert.Equal("prompt-1", prompts.CommunityAgentPrompts[0].Id);
    Assert.Equal("community-1", prompts.CommunityAgentPrompts[0].CommunityId);
    Assert.Equal("moderator-1", prompts.CommunityAgentPrompts[0].CreatedById);
    Assert.Equal("agent-1", prompts.CommunityAgentPrompts[0].AgentId);
    Assert.Equal("Prompt", prompts.CommunityAgentPrompts[0].Prompt);
    Assert.Equal("gpt-test", prompts.CommunityAgentPrompts[0].ModelName);
    Assert.Equal("openai", prompts.CommunityAgentPrompts[0].ModelProvider);
    Assert.True(prompts.CommunityAgentPrompts[0].SlotAllocated);
    Assert.Equal("queue", prompts.CommunityAgentPrompts[0].OnFlagAction);
    Assert.Equal(createdAt, prompts.CommunityAgentPrompts[0].ActivatedAt);
    Assert.Equal(createdAt, prompts.CommunityAgentPrompts[0].CreatedAt);
    Assert.Equal(updatedAt, prompts.CommunityAgentPrompts[0].UpdatedAt);
    Assert.Equal(1, prompts.SlotInfo["available"]);
    Assert.Equal("history-1", historyResponse.Entries[0].Id);
    Assert.Equal("prompt-1", historyResponse.Entries[0].AgentPromptId);
    Assert.Equal("community-1", historyResponse.Entries[0].CommunityId);
    Assert.Equal("update", historyResponse.Entries[0].Action);
    Assert.Equal("mod", historyResponse.Entries[0].ChangedBy!.Username);
    Assert.Equal("old", historyResponse.Entries[0].PreviousFields["prompt"]);
    Assert.Equal("new", historyResponse.Entries[0].NextFields["prompt"]);
    Assert.Equal(true, historyResponse.Entries[0].ChangedFields["prompt"]);
    Assert.Equal("post-1", simulation.Results[0].PostId);
    Assert.Equal("Title", simulation.Results[0].Title);
    Assert.Equal("review", simulation.Results[0].PostType);
    Assert.Equal(createdAt, simulation.Results[0].ApprovedAt);
    Assert.Equal("Excerpt", simulation.Results[0].ContentExcerpt);
    Assert.True(simulation.Results[0].Flagged);
    Assert.Equal("spam", simulation.Results[0].Reason);
    Assert.Equal("prompt-1", simulation.Simulation.PromptId);
    Assert.Equal(24, simulation.Simulation.TimeWindowHours);
    Assert.Equal(5, simulation.Simulation.SampleCount);
    Assert.Equal(1, simulation.Simulation.WouldFlagCount);
    Assert.Equal(1, simulation.Simulation.WouldUnpublishCount);
    Assert.Equal(10, simulation.Simulation.FalsePositiveEstimate!.HistoricalFlaggedCount);
    Assert.Equal(2, simulation.Simulation.FalsePositiveEstimate.HistoricalApprovedCount);
    Assert.Equal(0.2m, simulation.Simulation.FalsePositiveEstimate.Rate);
    Assert.True(simulation.Results[0].WouldUnpublish);
    Assert.Equal("community", analytics.Scope.Type);
    Assert.Equal("community-1", analytics.Scope.CommunityId);
    Assert.Equal("30d", analytics.Range);
    Assert.Equal(createdAt.AddDays(-30), analytics.PeriodStart);
    Assert.Equal(createdAt, analytics.PeriodEnd);
    Assert.Equal(5, analytics.QueueVolume.TotalReports);
    Assert.Equal(2, analytics.QueueVolume.PendingReports);
    Assert.Equal("reports", analytics.QueueVolume.ReportsOverTime[0]);
    Assert.Equal("clearance", analytics.QueueVolume.ClearanceActionsOverTime[0]);
    Assert.Equal("moderator", analytics.QueueVolume.ModeratorActionsOverTime[0]);
    Assert.Equal(3, analytics.RuleViolations["spam"]);
    Assert.Equal(4, analytics.Appeals.TotalClosed);
    Assert.Equal(1, analytics.Appeals.Accepted);
    Assert.Equal(1, analytics.Appeals.Reduced);
    Assert.Equal(1, analytics.Appeals.Denied);
    Assert.Equal(1, analytics.Appeals.Dismissed);
    Assert.Equal(0.5m, analytics.Appeals.SuccessRate);
    Assert.Equal("moderator", analytics.ModeratorWorkload.Moderators[0]);
    Assert.Equal("mod", analytics.ModeratorWorkload.Users["moderator-1"].Username);
    Assert.Equal(6, analytics.AutomodPerformance.TotalActions);
    Assert.Equal(3, analytics.AutomodPerformance.AutoRemoves);
    Assert.Equal(2, analytics.AutomodPerformance.ReviewedCount);
    Assert.Equal(1, analytics.AutomodPerformance.FalsePositiveCount);
    Assert.Equal(0.5m, analytics.AutomodPerformance.FalsePositiveRate);
    Assert.Equal("actions", analytics.AutomodPerformance.ActionsOverTime[0]);
    Assert.Equal("confidence", analytics.AutomodPerformance.ConfidenceDistribution[0]);
    Assert.Equal("sources", analytics.AutomodPerformance.Sources[0]);
    Assert.Equal(8, analytics.NewUserFriction.FirstPosts);
    Assert.Equal(2, analytics.NewUserFriction.RejectedFirstPosts);
    Assert.Equal(0.25m, analytics.NewUserFriction.RejectionRate);
    Assert.Equal("thread-1", threads.Results[0].Id);
    Assert.Equal("community-1", threads.Results[0].CommunityId);
    Assert.Equal("user-1", threads.Results[0].SubjectUserId);
    Assert.Equal("moderator-1", threads.Results[0].AssignedModId);
    Assert.Equal(createdAt, threads.Results[0].CreatedAt);
    Assert.Equal(updatedAt, threads.Results[0].UpdatedAt);
    Assert.Equal("message-1", messages.Results[0].Id);
    Assert.Equal("thread-1", messages.Results[0].ConversationId);
    Assert.Equal("Hello", messages.Results[0].BodyText);
    Assert.Equal("moderator-1", messages.Results[0].CreatedById);
    Assert.Equal("mod", messages.Results[0].SenderUsername);
    Assert.Equal("reply-1", savedReplies.Results[0].Id);
    Assert.Equal("community-1", savedReplies.Results[0].CommunityId);
    Assert.Equal("Greeting", savedReplies.Results[0].Title);
    Assert.Equal("Hello", savedReplyResponse.Reply.Body);
  }

  [Fact]
  public void CommunityAutomodRecentActionsEndpointIncludesOptionalPaging()
  {
    var endpoint = VouchaApiEndpoints.CommunityAutomodRecentActions("test community", "cursor 1", 12);

    Assert.Equal(HttpMethod.Get, endpoint.Method);
    Assert.Equal("/api/v1/communities/test%20community/automod/recent-actions", endpoint.Path);
    Assert.Equal(
        [
          new KeyValuePair<string, string>("after", "cursor 1"),
          new KeyValuePair<string, string>("limit", "12"),
          new KeyValuePair<string, string>("source", "agent_moderation"),
          new KeyValuePair<string, string>("window", "24h"),
        ],
        endpoint.Query);
  }
}
