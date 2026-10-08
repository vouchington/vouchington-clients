using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class ApiCommunitiesServiceTests
{
  [Fact]
  public async Task CommunityServiceCoversAgentAndAutomodWrappers()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.ai-agents.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.agent-prompts.default")),
        new RecordedResponse("""
            {
              "entries": [
                {
                  "id": "history-1",
                  "agent_prompt_id": "prompt-1",
                  "community_id": "community-1",
                  "action": "create",
                  "changed_by": null,
                  "previous_fields": {},
                  "next_fields": {},
                  "changed_fields": {},
                  "created_at": "2026-07-01T00:00:00Z"
                }
              ],
              "next_cursor": "cursor-2"
            }
            """),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.automod-simulate.default")),
        new RecordedResponse("""{"applied_action":true}"""),
        new RecordedResponse("""{"community_restrictions":{}}"""),
    ]);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));

    var aiAgents = await service.FetchAiAgentsAsync("test-community", TestContext.Current.CancellationToken);
    var prompts = await service.FetchAgentPromptsAsync("test-community", TestContext.Current.CancellationToken);
    var history = await service.FetchAgentPromptHistoryAsync("test-community", TestContext.Current.CancellationToken);
    var simulation = await service.SimulateAutomodAsync(
        "test-community",
        new CommunityAutomodSimulationRequest("prompt-1", "Prompt body", 12, 5),
        TestContext.Current.CancellationToken);
    var feedback = await service.RecordAutomodFeedbackAsync(
        "test-community",
        "source-key",
        new CommunityAutomodFeedbackRequest("approved", "remove", "spam", "note"),
        TestContext.Current.CancellationToken);
    await service.ActivateRestrictionsAsync(
        "test-community",
        new ActivateCommunityRestrictionsRequest(["spam"], Reason: "raid"),
        TestContext.Current.CancellationToken);

    Assert.NotEmpty(aiAgents.CommunityAiAgents);
    Assert.NotEmpty(prompts.CommunityAgentPrompts);
    Assert.Equal("cursor-2", history.NextCursor);
    Assert.NotNull(simulation.Results);
    Assert.True(feedback.AppliedAction);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/test-community/ai-agents", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/agent-prompts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/agent-prompts/history", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/automod/simulate", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/automod/recent-actions/source-key/feedback", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/restrictions", request.PathAndQuery));
    Assert.Contains("\"prompt_id\":\"prompt-1\"", handler.Requests[3].Body!, StringComparison.Ordinal);
    Assert.Contains("\"outcome\":\"approved\"", handler.Requests[4].Body!, StringComparison.Ordinal);
    Assert.Contains("\"restriction_types\":[\"spam\"]", handler.Requests[5].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CommunityServiceCoversModerationAndSavedReplyWrappers()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.saved-replies.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.saved-reply.create.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.automod-recent-actions.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.automod-feedback.create.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.moderation-results.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.warning.create.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.post-type-settings.update.default")),
    ]);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));

    var savedReplies = await service.FetchSavedRepliesAsync("test-community", TestContext.Current.CancellationToken);
    var createdReply = await service.CreateSavedReplyAsync(
        "test-community",
        new CreateCommunitySavedReplyRequest("Greeting", "Thanks for writing in."),
        TestContext.Current.CancellationToken);
    var recentActions = await service.FetchAutomodRecentActionsAsync("test-community", cancellationToken: TestContext.Current.CancellationToken);
    var feedback = await service.RecordAutomodFeedbackAsync(
        "test-community",
        "agent_moderation:post-1",
        new CommunityAutomodFeedbackRequest("true_positive", "keep_removed", "correct", "Matches the community rules."),
        TestContext.Current.CancellationToken);
    var moderationResults = await service.FetchModerationResultsAsync("test-community", "post-1", TestContext.Current.CancellationToken);
    var warning = await service.IssueWarningAsync(
        "test-community",
        new IssueCommunityWarningRequest("user-1", "Spam in community", "Please read the community rules.", ResolveReport: false),
        TestContext.Current.CancellationToken);
    var postTypeSettings = await service.UpdatePostTypeSettingsAsync(
        "test-community",
        new UpdateCommunityPostTypeSettingsRequest(true, true),
        TestContext.Current.CancellationToken);

    Assert.NotEmpty(savedReplies.Results);
    Assert.Equal("00000000-0000-7000-8000-000000000700", createdReply.Id);
    Assert.NotEmpty(recentActions.AutomodActions);
    Assert.True(feedback.AppliedAction);
    Assert.Equal(AdminReviewQueueClearanceStatus.InReview, moderationResults.PlatformModeration.Status);
    Assert.Equal("warning-1", warning.Warning.Id);
    Assert.NotNull(postTypeSettings.Community);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/test-community/saved-replies", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/saved-replies", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/automod/recent-actions?limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/automod/recent-actions/agent_moderation%3Apost-1/feedback", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/posts/post-1/moderation-results", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/warnings", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/post-type-settings", request.PathAndQuery));
  }

  [Fact]
  public async Task CommunityServiceCoversAdminModerationWrappers()
  {
    var handler = new RecordingHandler([
        new RecordedResponse("""
            {"thread":{"id":"thread-1","community_id":"community-1","subject_user_id":"user-1","assigned_moderator_user_id":null,"resolved_at":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"conversation":{"id":"conversation-1","channel_type":"direct_message"}}
            """),
        new RecordedResponse("""
            {"message":{"id":"message-1","conversation_id":"thread-1","body_text":"hello","created_by_id":"moderator-1","sender_username":"mod","created_at":"2026-07-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"thread":{"id":"thread-1","community_id":"community-1","subject_user_id":"user-1","assigned_moderator_user_id":"moderator-1","resolved_at":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-02T00:00:00Z"}}
            """),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-1","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Welcome","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"activated_at":null,"deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-2","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Created","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"activated_at":null,"deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-3","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Updated","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"activated_at":null,"deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-02T00:00:00Z"}}
            """),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("""{"ok":true}"""),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
    ]);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));

    var thread = await service.OpenModmailAsync("test community", new OpenCommunityModmailRequest("user-1"), TestContext.Current.CancellationToken);
    var conversation = await service.OpenModmailForReportAsync("test community", "report-1", TestContext.Current.CancellationToken);
    var message = await service.SendModmailMessageAsync("test community", "thread-1", "hello", TestContext.Current.CancellationToken);
    var updatedThread = await service.UpdateModmailThreadAsync(
        "test community",
        "thread-1",
        new UpdateCommunityModmailThreadRequest("moderator-1", true),
        TestContext.Current.CancellationToken);
    await service.ResolveModerationReportAsync("test community", "report-1", "resolved", TestContext.Current.CancellationToken);
    await service.EnableAiAgentAsync("test community", "agent-1", TestContext.Current.CancellationToken);
    await service.DisableAiAgentAsync("test community", "agent-1", TestContext.Current.CancellationToken);
    var prompt = await service.FetchAgentPromptAsync("test community", "prompt-1", TestContext.Current.CancellationToken);
    var createdPrompt = await service.CreateAgentPromptAsync(
        "test community",
        new CommunityAgentPromptUpsertRequest("Created", "gpt-5.4-nano", "openai"),
        TestContext.Current.CancellationToken);
    var updatedPrompt = await service.UpdateAgentPromptAsync(
        "test community",
        "prompt-3",
        new CommunityAgentPromptUpdateRequest("Updated"),
        TestContext.Current.CancellationToken);
    await service.DeleteAgentPromptAsync("test community", "prompt-3", TestContext.Current.CancellationToken);
    await service.AllocateAgentPromptSlotAsync("test community", "prompt-3", TestContext.Current.CancellationToken);
    await service.DeallocateAgentPromptSlotAsync("test community", "prompt-3", TestContext.Current.CancellationToken);
    var testRun = await service.TestAgentPromptAsync(
        "test community",
        "prompt-3",
        new CommunityAgentPromptTestRunRequest("body", true, false),
        TestContext.Current.CancellationToken);
    await service.ConfirmBanEvasionAsync("test community", "user-1", TestContext.Current.CancellationToken);
    await service.DismissBanEvasionAsync("test community", "user-1", TestContext.Current.CancellationToken);

    Assert.Equal("thread-1", thread.Id);
    Assert.Equal("conversation-1", conversation.Id);
    Assert.Equal("message-1", message.Id);
    Assert.Equal("thread-1", updatedThread.Id);
    Assert.Equal("moderator-1", updatedThread.AssignedModId);
    Assert.Equal("prompt-1", prompt.Id);
    Assert.Equal("prompt-2", createdPrompt.Id);
    Assert.Equal("prompt-3", updatedPrompt.Id);
    Assert.Equal(System.Text.Json.JsonValueKind.Object, testRun.ValueKind);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/test%20community/modmail", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/reports/report-1/modmail", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/modmail/thread-1/messages", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/modmail/thread-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/reports/report-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/ai-agents/agent-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/ai-agents/agent-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-3", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-3", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-3/allocations", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-3/allocations", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt-3/test-runs", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/ban-evasion/user-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test%20community/ban-evasion/user-1", request.PathAndQuery));
    Assert.Contains("\"subject_user_id\":\"user-1\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Contains("\"assigned_moderator_user_id\":\"moderator-1\"", handler.Requests[3].Body!, StringComparison.Ordinal);
    using var assignmentBody = System.Text.Json.JsonDocument.Parse(handler.Requests[3].Body!);
    Assert.False(assignmentBody.RootElement.TryGetProperty("assigned_mod_id", out _));
    Assert.Contains("\"resolved\":true", handler.Requests[3].Body!, StringComparison.Ordinal);
    Assert.Contains("\"status\":\"resolved\"", handler.Requests[4].Body!, StringComparison.Ordinal);
    Assert.Contains("\"prompt\":\"Created\"", handler.Requests[8].Body!, StringComparison.Ordinal);
    Assert.Contains("\"prompt\":\"Updated\"", handler.Requests[9].Body!, StringComparison.Ordinal);
    Assert.Contains("\"text\":\"body\"", handler.Requests[13].Body!, StringComparison.Ordinal);
  }
}
