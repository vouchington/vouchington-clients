using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public partial interface ICommunitiesService
{
  Task<CommunityAutomodActionsResponse> FetchAutomodRecentActionsAsync(
      string idOrSlug,
      string? after = null,
      int? limit = 25,
      string? window = null,
      string? source = null,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunitySavedRepliesResponse> FetchSavedRepliesAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunitySavedRepliesResponse> FetchSavedRepliesPageAsync(
      string idOrSlug,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      FetchSavedRepliesAsync(idOrSlug, cancellationToken);

  Task<CommunitySavedReply> CreateSavedReplyAsync(
      string idOrSlug,
      CreateCommunitySavedReplyRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task DeleteSavedReplyAsync(string idOrSlug, string replyId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityModmailThread> OpenModmailAsync(string idOrSlug, OpenCommunityModmailRequest request, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityReportModmailConversation> OpenModmailForReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityModmailMessage> SendModmailMessageAsync(
      string idOrSlug,
      string conversationId,
      string text,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityModmailThread> UpdateModmailThreadAsync(
      string idOrSlug,
      string conversationId,
      UpdateCommunityModmailThreadRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityMutationResponse> UpdatePostTypeSettingsAsync(
      string idOrSlug,
      UpdateCommunityPostTypeSettingsRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityWarningResponse> IssueWarningAsync(
      string idOrSlug,
      IssueCommunityWarningRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task ResolveModerationReportAsync(
      string idOrSlug,
      string reportId,
      string status,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityModerationResultsResponse> FetchModerationResultsAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task EnableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task DisableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityAgentPrompt> FetchAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityAgentPrompt> CreateAgentPromptAsync(
      string idOrSlug,
      CommunityAgentPromptUpsertRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<CommunityAgentPrompt> UpdateAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptUpdateRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task DeleteAgentPromptAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task AllocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task DeallocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<JsonElement> TestAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptTestRunRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task ConfirmBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task DismissBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
}
