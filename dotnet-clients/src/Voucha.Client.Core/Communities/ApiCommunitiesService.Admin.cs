using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task<CommunityModmailMessageListResponse> FetchModmailMessagesAsync(
      string idOrSlug,
      string threadId,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModmailMessagesAsync(idOrSlug, threadId, cancellationToken: cancellationToken);

  public Task<CommunityModmailThreadListResponse> FetchModmailAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModmailAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityModmailMessageListResponse> FetchModmailMessagesPageAsync(
      string idOrSlug,
      string threadId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModmailMessagesAsync(idOrSlug, threadId, after, limit, cancellationToken);

  public Task<CommunityModmailThreadListResponse> FetchModmailPageAsync(
      string idOrSlug,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModmailAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityAutomodActionsResponse> FetchAutomodRecentActionsAsync(
      string idOrSlug,
      string? after = null,
      int? limit = 25,
      string? window = null,
      string? source = null,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityAutomodRecentActionsAsync(idOrSlug, after, limit, window, source, cancellationToken);

  public Task<CommunitySavedRepliesResponse> FetchSavedRepliesAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunitySavedRepliesAsync(idOrSlug, cancellationToken);

  public Task<CommunitySavedRepliesResponse> FetchSavedRepliesPageAsync(
      string idOrSlug,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunitySavedRepliesPageAsync(idOrSlug, after, limit, cancellationToken);

  public async Task<CommunitySavedReply> CreateSavedReplyAsync(
      string idOrSlug,
      CreateCommunitySavedReplyRequest request,
      CancellationToken cancellationToken = default) =>
      (await client.CreateCommunitySavedReplyAsync(idOrSlug, request, cancellationToken).ConfigureAwait(true)).Reply;

  public Task DeleteSavedReplyAsync(string idOrSlug, string replyId, CancellationToken cancellationToken = default) =>
      client.DeleteCommunitySavedReplyAsync(idOrSlug, replyId, cancellationToken);

  public Task<CommunityModmailThread> OpenModmailAsync(
      string idOrSlug,
      OpenCommunityModmailRequest request,
      CancellationToken cancellationToken = default) =>
      client.OpenCommunityModmailAsync(idOrSlug, request, cancellationToken);

  public Task<CommunityReportModmailConversation> OpenModmailForReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      client.OpenCommunityModmailForReportAsync(idOrSlug, reportId, cancellationToken);

  public Task<CommunityModmailMessage> SendModmailMessageAsync(
      string idOrSlug,
      string conversationId,
      string text,
      CancellationToken cancellationToken = default) =>
      client.SendCommunityModmailMessageAsync(idOrSlug, conversationId, new { text }, cancellationToken);

  public Task<CommunityModmailThread> UpdateModmailThreadAsync(
      string idOrSlug,
      string conversationId,
      UpdateCommunityModmailThreadRequest request,
      CancellationToken cancellationToken = default) =>
      client.UpdateCommunityModmailThreadAsync(idOrSlug, conversationId, request, cancellationToken);

  public Task<CommunityMutationResponse> UpdatePostTypeSettingsAsync(
      string idOrSlug,
      UpdateCommunityPostTypeSettingsRequest request,
      CancellationToken cancellationToken = default) =>
      client.UpdateCommunityPostTypeSettingsAsync(idOrSlug, request, cancellationToken);

  public Task<CommunityWarningResponse> IssueWarningAsync(
      string idOrSlug,
      IssueCommunityWarningRequest request,
      CancellationToken cancellationToken = default) =>
      client.IssueCommunityWarningAsync(idOrSlug, request, cancellationToken);

  public Task ResolveModerationReportAsync(
      string idOrSlug,
      string reportId,
      string status,
      CancellationToken cancellationToken = default) =>
      client.ResolveCommunityModerationReportAsync(idOrSlug, reportId, status, cancellationToken);

  public Task<CommunityModerationResultsResponse> FetchModerationResultsAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModerationResultsAsync(idOrSlug, postId, cancellationToken);

  public Task EnableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default) =>
      client.EnableCommunityAiAgentAsync(idOrSlug, agentSlug, cancellationToken);

  public Task DisableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default) =>
      client.DeleteCommunityAiAgentAsync(idOrSlug, agentSlug, cancellationToken);

  public Task<CommunityAgentPrompt> FetchAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityAgentPromptAsync(idOrSlug, promptId, cancellationToken);

  public Task<CommunityAgentPrompt> CreateAgentPromptAsync(
      string idOrSlug,
      CommunityAgentPromptUpsertRequest request,
      CancellationToken cancellationToken = default) =>
      client.CreateCommunityAgentPromptAsync(idOrSlug, request, cancellationToken);

  public Task<CommunityAgentPrompt> UpdateAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptUpdateRequest request,
      CancellationToken cancellationToken = default) =>
      client.UpdateCommunityAgentPromptAsync(idOrSlug, promptId, request, cancellationToken);

  public Task DeleteAgentPromptAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      client.DeleteCommunityAgentPromptAsync(idOrSlug, promptId, cancellationToken);

  public Task AllocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      client.AllocateCommunityAgentPromptSlotAsync(idOrSlug, promptId, cancellationToken);

  public Task DeallocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      client.DeallocateCommunityAgentPromptSlotAsync(idOrSlug, promptId, cancellationToken);

  public Task<JsonElement> TestAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptTestRunRequest request,
      CancellationToken cancellationToken = default) =>
      client.TestCommunityAgentPromptAsync(idOrSlug, promptId, request, cancellationToken);

  public Task ConfirmBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.ConfirmCommunityBanEvasionAsync(idOrSlug, userId, cancellationToken);

  public Task DismissBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.DismissCommunityBanEvasionAsync(idOrSlug, userId, cancellationToken);
}
