using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task CommunityAdministrationClientMethodsUseExpectedRoutesAndBodies()
  {
    var handler = new RecordingHandler(RepeatEmptyResponses(45));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchCommunityApplicationsAsync("test community", "cursor-1", 7, token);
    await client.FetchCommunityApplicationQuestionsAsync("test community", token);
    await client.FetchCommunityInvitesAsync("test community", "cursor-2", 8, token);
    await client.FetchCommunityBansAsync("test community", "cursor-3", 9, token);
    await client.FetchCommunityRestrictionsAsync("test community", "cursor-4", token);
    await client.ActivateCommunityRestrictionsAsync(
        "test community",
        new ActivateCommunityRestrictionsRequest(["invite_only"], Reason: "raid"),
        token);
    await client.FetchCommunityModlogAsync("test community", "cursor-5", "ban", token);
    await client.FetchCommunityModeratorVacationAsync("test community", token);
    await client.SetCommunityModeratorVacationAsync("test community", DateTimeOffset.Parse("2026-01-02T03:04:05Z"), token);
    await client.ClearCommunityModeratorVacationAsync("test community", token);
    await client.FetchCommunityModmailAsync("test community", "cursor-6", 12, token);
    await client.OpenCommunityModmailAsync("test community", new { subject_user_id = "user-1" }, token);
    await client.OpenCommunityModmailForReportAsync("test community", "report 2", token);
    await client.FetchCommunityModmailMessagesAsync("test community", "thread 1", "cursor-7", 13, token);
    await client.SendCommunityModmailMessageAsync("test community", "thread 1", new { text = "hello" }, token);
    await client.UpdateCommunityModmailThreadAsync("test community", "thread 1", new { resolved = true }, token);
    await client.FetchCommunitySavedRepliesAsync("test community", token);
    await client.CreateCommunitySavedReplyAsync("test community", new { title = "Hi", body = "Hello" }, token);
    await client.DeleteCommunitySavedReplyAsync("test community", "reply 1", token);
    await client.FetchCommunityAiAgentsAsync("test community", token);
    await client.EnableCommunityAiAgentAsync("test community", "agent 1", token);
    await client.DeleteCommunityAiAgentAsync("test community", "agent 1", token);
    await client.FetchCommunityAgentPromptsAsync("test community", token);
    await client.FetchCommunityAgentPromptAsync("test community", "prompt 1", token);
    await client.CreateCommunityAgentPromptAsync(
        "test community",
        new CommunityAgentPromptUpsertRequest("Prompt", "gpt-test", "openai"),
        token);
    await client.UpdateCommunityAgentPromptAsync(
        "test community",
        "prompt 1",
        new CommunityAgentPromptUpdateRequest("Updated"),
        token);
    await client.DeleteCommunityAgentPromptAsync("test community", "prompt 1", token);
    await client.AllocateCommunityAgentPromptSlotAsync("test community", "prompt 1", token);
    await client.DeallocateCommunityAgentPromptSlotAsync("test community", "prompt 1", token);
    await client.TestCommunityAgentPromptAsync(
        "test community",
        "prompt 1",
        new CommunityAgentPromptTestRunRequest("body", true, false, "spam"),
        token);
    await client.FetchCommunityAgentPromptHistoryAsync("test community", "prompt 1", "cursor-8", token);
    await client.RecordCommunityAutomodFeedbackAsync(
        "test community",
        "source 1",
        new CommunityAutomodFeedbackRequest("false_positive", "keep_removed", "spam", "note"),
        token);
    await client.SimulateCommunityAutomodAsync(
        "test community",
        new CommunityAutomodSimulationRequest("prompt-1", "Prompt", 24, 10),
        token);
    await client.FetchCommunityModerationQueueAsync("test community", "cursor-9", 11, token);
    await client.ClaimCommunityModerationReportAsync("test community", "report 1", token);
    await client.ReleaseCommunityModerationReportAsync("test community", "report 1", token);
    await client.ClaimCommunityModerationPostAsync("test community", "post 1", token);
    await client.ReleaseCommunityModerationPostAsync("test community", "post 1", token);
    await client.OpenCommunityModerationReportThreadAsync("test community", "report 1", token);
    await client.OpenCommunityModerationPostThreadAsync("test community", "post 1", token);
    await client.EscalateCommunityModerationReportAsync("test community", "report 1", token);
    await client.DeescalateCommunityModerationReportAsync("test community", "report 1", token);
    await client.EscalateCommunityModerationPostAsync("test community", "post 1", token);
    await client.DeescalateCommunityModerationPostAsync("test community", "post 1", token);
    await client.FetchCommunityModerationAnalyticsAsync("test community", "90d", token);

    Assert.Equal("/api/v1/communities/test%20community/applications?after=cursor-1&limit=7", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/restrictions", handler.Requests[5].PathAndQuery);
    Assert.Contains("\"restriction_types\":[\"invite_only\"]", handler.Requests[5].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/modlog?action_type=ban&after=cursor-5", handler.Requests[6].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/modmail?after=cursor-6&limit=12", handler.Requests[10].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/reports/report%202/modmail", handler.Requests[12].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/modmail/thread%201/messages?after=cursor-7&limit=13", handler.Requests[13].PathAndQuery);
    Assert.Contains("\"text\":\"hello\"", handler.Requests[14].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/ai-agents/agent%201", handler.Requests[20].PathAndQuery);
    Assert.Equal(HttpMethod.Put, handler.Requests[20].Method);
    Assert.Equal("/api/v1/communities/test%20community/agent-prompts/prompt%201/test-runs", handler.Requests[29].PathAndQuery);
    Assert.Contains("\"save_for_training\":true", handler.Requests[29].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/automod/simulate", handler.Requests[32].PathAndQuery);
    Assert.Contains("\"prompt_id\":\"prompt-1\"", handler.Requests[32].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/reports/report%201/escalation", handler.Requests[40].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/moderation-analytics?range=90d", handler.Requests[44].PathAndQuery);
  }

  [Fact]
  public async Task CommunityAdministrationClientMethodsCoverNewModerationRoutes()
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
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchCommunitySavedRepliesAsync("test community", token);
    await client.CreateCommunitySavedReplyAsync("test community", new CreateCommunitySavedReplyRequest("Greeting", "Thanks for writing in."), token);
    await client.FetchCommunityAutomodRecentActionsAsync("test community", cancellationToken: token);
    await client.RecordCommunityAutomodFeedbackAsync(
        "test community",
        "agent_moderation:post-1",
        new CommunityAutomodFeedbackRequest("true_positive", "keep_removed", "correct", "Matches the community rules."),
        token);
    await client.FetchCommunityModerationResultsAsync("test community", "post-1", token);
    await client.IssueCommunityWarningAsync(
        "test community",
        new IssueCommunityWarningRequest("user-1", "Spam in community", "Please read the community rules.", ResolveReport: false),
        token);
    await client.UpdateCommunityPostTypeSettingsAsync("test community", new UpdateCommunityPostTypeSettingsRequest(true, true), token);

    Assert.Equal("/api/v1/communities/test%20community/saved-replies", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/saved-replies", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"title\":\"Greeting\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/automod/recent-actions?limit=25", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/automod/recent-actions/agent_moderation%3Apost-1/feedback", handler.Requests[3].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/posts/post-1/moderation-results", handler.Requests[4].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/warnings", handler.Requests[5].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/post-type-settings", handler.Requests[6].PathAndQuery);
  }

  [Fact]
  public async Task CommunityListItemAdderUsesTypedBackendRoutes()
  {
    var handler = new RecordingHandler(RepeatEmptyResponses(5));
    var service = new ApiCommunitiesService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var token = TestContext.Current.CancellationToken;

    await service.AddListItemAsync("test community", new CommunityListItemRequest(TopicId: "topic-1"), token);
    await service.AddListItemAsync("test community", new CommunityListItemRequest(RssFeedId: "feed-1"), token);
    await service.AddListItemAsync("test community", new CommunityListItemRequest(PostId: "post-1"), token);
    await service.AddListItemAsync("test community", new CommunityListItemRequest(UrlHostnameId: "hostname-1"), token);
    await service.AddListItemAsync("test community", new CommunityListItemRequest(UrlId: "url-1"), token);

    Assert.Equal("/api/v1/communities/test%20community/list-items/topics", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"topic_id\":\"topic-1\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/list-items/rss-feeds", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"rss_feed_id\":\"feed-1\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/list-items/posts", handler.Requests[2].PathAndQuery);
    Assert.Contains("\"post_id\":\"post-1\"", handler.Requests[2].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/list-items/domains", handler.Requests[3].PathAndQuery);
    Assert.Contains("\"url_hostname_id\":\"hostname-1\"", handler.Requests[3].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/list-items/urls", handler.Requests[4].PathAndQuery);
    Assert.Contains("\"url_id\":\"url-1\"", handler.Requests[4].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CommunityAdministrationMutationsUnwrapEnvelopeResponses()
  {
    var handler = new RecordingHandler([
        new RecordedResponse("""
            {"thread":{"id":"thread-1","community_id":"community-1","subject_user_id":"user-1","assigned_mod_id":null,"resolved_at":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"conversation":{"id":"conversation-1","channel_type":"direct_message"}}
            """),
        new RecordedResponse("""
            {"message":{"id":"message-1","conversation_id":"thread-1","body_text":"hello","created_by_id":"moderator-1","sender_username":"mod","created_at":"2026-01-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"thread":{"id":"thread-1","community_id":"community-1","subject_user_id":"user-1","assigned_mod_id":"moderator-1","resolved_at":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-1","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Welcome","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"on_flag_action":"none","activated_at":"2026-01-01T00:00:00Z","deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-2","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Updated","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"on_flag_action":"none","activated_at":"2026-01-01T00:00:00Z","deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z"}}
            """),
        new RecordedResponse("""
            {"community_agent_prompt":{"id":"prompt-3","community_id":"community-1","created_by_id":"user-1","agent_id":"agent-1","prompt":"Patched","model_name":"gpt-5.4-nano","model_provider":"openai","slot_allocated":true,"on_flag_action":"none","activated_at":"2026-01-01T00:00:00Z","deactivated_at":null,"deleted_at":null,"deleted_by_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-03T00:00:00Z"}}
            """),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    var thread = await client.OpenCommunityModmailAsync("test community", new { subject_user_id = "user-1" }, token);
    var reportConversation = await client.OpenCommunityModmailForReportAsync("test community", "report-1", token);
    var message = await client.SendCommunityModmailMessageAsync("test community", "thread-1", new { text = "hello" }, token);
    var updatedThread = await client.UpdateCommunityModmailThreadAsync("test community", "thread-1", new { resolved = true }, token);
    var prompt = await client.FetchCommunityAgentPromptAsync("test community", "prompt-1", token);
    var createdPrompt = await client.CreateCommunityAgentPromptAsync(
        "test community",
        new CommunityAgentPromptUpsertRequest("Welcome", "gpt-5.4-nano", "openai"),
        token);
    var patchedPrompt = await client.UpdateCommunityAgentPromptAsync(
        "test community",
        "prompt-3",
        new CommunityAgentPromptUpdateRequest("Patched"),
        token);

    Assert.Equal("thread-1", thread.Id);
    Assert.Equal("conversation-1", reportConversation.Id);
    Assert.Equal("direct_message", reportConversation.ChannelType);
    Assert.Equal("message-1", message.Id);
    Assert.Equal("thread-1", updatedThread.Id);
    Assert.Equal("prompt-1", prompt.Id);
    Assert.Equal("prompt-2", createdPrompt.Id);
    Assert.Equal("prompt-3", patchedPrompt.Id);
  }

  [Fact]
  public async Task ApiCommunitiesServiceDelegatesAdministrationMethodsToClientRoutes()
  {
    var handler = new RecordingHandler(RepeatEmptyResponses(26));
    var service = new ApiCommunitiesService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var token = TestContext.Current.CancellationToken;

    await service.SearchAsync("test", cancellationToken: token);
    await service.UpdateMemberRoleAsync("test community", "user 1", "moderator", token);
    await service.RemoveMemberAsync("test community", "user 1", token);
    await service.TransferOwnershipAsync("test community", "user 2", token);
    await service.AddListItemAsync("test community", new CommunityListItemRequest(TopicId: "topic-1"), token);
    await service.RemoveListItemAsync("test community", "topics", "topic 1", token);
    await service.ReviewApplicationAsync(
        "test community",
        "application 1",
        new UpdateCommunityPostReviewRequest("approved"),
        token);
    await service.SendInviteAsync("test community", new SendCommunityInviteRequest("test@example.com"), token);
    await service.RevokeInviteAsync("test community", "invite 1", token);
    await service.RedeemInviteAsync(new RedeemCommunityInviteRequest("code-1"), token);
    await service.ReviewPostAsync("test community", "post 1", new UpdateCommunityPostReviewRequest("rejected", "spam"), token);
    await service.UpdatePinnedPostsAsync("test community", new CommunityPinnedPostsUpdateRequest(["post-1"]), token);
    await service.BanAsync("test community", new BanCommunityMemberRequest("user-3"), token);
    await service.LiftBanAsync("test community", "user 3", token);
    await service.ActivateRestrictionsAsync(
        "test community",
        new ActivateCommunityRestrictionsRequest(["invite_only"]),
        token);
    await service.LiftRestrictionAsync("test community", "restriction 1", token);
    await service.SetModeratorVacationAsync("test community", null, token);
    await service.ClearModeratorVacationAsync("test community", token);
    await service.ClaimModerationReportAsync("test community", "report 1", token);
    await service.ReleaseModerationReportAsync("test community", "report 1", token);
    await service.ClaimModerationPostAsync("test community", "post 1", token);
    await service.ReleaseModerationPostAsync("test community", "post 1", token);
    await service.OpenModerationReportThreadAsync("test community", "report 1", token);
    await service.OpenModerationPostThreadAsync("test community", "post 1", token);
    await service.EscalateModerationReportAsync("test community", "report 1", token);
    await service.DeescalateModerationPostAsync("test community", "post 1", token);

    Assert.Equal("/api/v1/communities?q=test", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/members/user%201", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"role\":\"moderator\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/test%20community/list-items/topics", handler.Requests[4].PathAndQuery);
    Assert.Contains("\"topic_id\":\"topic-1\"", handler.Requests[4].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/communities/invite-redemptions", handler.Requests[9].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/posts/post%201", handler.Requests[10].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/restrictions/restriction%201", handler.Requests[15].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/reports/report%201/claim", handler.Requests[18].PathAndQuery);
    Assert.Equal("/api/v1/communities/test%20community/posts/post%201/escalation", handler.Requests[25].PathAndQuery);
  }

  private static IEnumerable<RecordedResponse> RepeatEmptyResponses(int count) =>
      Enumerable.Range(0, count).Select(_ => new RecordedResponse("{}"));
}
