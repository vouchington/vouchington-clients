namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityApplicationsResponse> FetchCommunityApplicationsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityApplicationsResponse>(
          VouchaApiEndpoints.CommunityApplications(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityApplicationQuestionsResponse> FetchCommunityApplicationQuestionsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityApplicationQuestionsResponse>(
          VouchaApiEndpoints.CommunityApplicationQuestions(idOrSlug),
          cancellationToken);

  public Task<CommunityInvitesResponse> FetchCommunityInvitesAsync(
      string idOrSlug,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityInvitesResponse>(
          VouchaApiEndpoints.CommunityInvites(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityBansResponse> FetchCommunityBansAsync(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityBansResponse>(
          VouchaApiEndpoints.CommunityBans(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityRestrictionsResponse> FetchCommunityRestrictionsAsync(
      string idOrSlug,
      string? after = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityRestrictionsResponse>(
          VouchaApiEndpoints.CommunityRestrictions(idOrSlug, after),
          cancellationToken);

  public Task<ActivateCommunityRestrictionsResponse> ActivateCommunityRestrictionsAsync(
      string idOrSlug,
      ActivateCommunityRestrictionsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<ActivateCommunityRestrictionsResponse>(
          VouchaApiEndpoints.ActivateCommunityRestrictions(idOrSlug, Require(request)),
          cancellationToken);

  public Task<ModlogResponse> FetchCommunityModlogAsync(
      string idOrSlug,
      string? after = null,
      string? actionType = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModlogResponse>(
          VouchaApiEndpoints.CommunityModlog(idOrSlug, after, actionType),
          cancellationToken);

  public Task<ModeratorVacationResponse> FetchCommunityModeratorVacationAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModeratorVacationResponse>(
          VouchaApiEndpoints.CommunityModeratorVacation(idOrSlug),
          cancellationToken);

  public Task<ModeratorVacationResponse> SetCommunityModeratorVacationAsync(
      string idOrSlug,
      DateTimeOffset? endsAt = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModeratorVacationResponse>(
          VouchaApiEndpoints.SetCommunityModeratorVacation(idOrSlug, endsAt),
          cancellationToken);

  public Task ClearCommunityModeratorVacationAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearCommunityModeratorVacation(idOrSlug), cancellationToken);

  public Task<CommunityModmailThreadListResponse> FetchCommunityModmailAsync(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModmailThreadListResponse>(
          VouchaApiEndpoints.CommunityModmail(idOrSlug, after, limit),
          cancellationToken);

  public async Task<CommunityModmailThread> OpenCommunityModmailAsync(
      string idOrSlug,
      object body,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityModmailThreadResponse>(
          VouchaApiEndpoints.OpenCommunityModmail(idOrSlug, body),
          cancellationToken).ConfigureAwait(false)).Thread;

  public async Task<CommunityReportModmailConversation> OpenCommunityModmailForReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityReportModmailConversationResponse>(
          VouchaApiEndpoints.OpenCommunityModmailForReport(idOrSlug, reportId),
          cancellationToken).ConfigureAwait(false)).Conversation;

  public Task<CommunityModmailMessageListResponse> FetchCommunityModmailMessagesAsync(
      string idOrSlug,
      string conversationId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModmailMessageListResponse>(
          VouchaApiEndpoints.CommunityModmailMessages(idOrSlug, conversationId, after, limit),
          cancellationToken);

  public async Task<CommunityModmailMessage> SendCommunityModmailMessageAsync(
      string idOrSlug,
      string conversationId,
      object body,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityModmailMessageResponse>(
          VouchaApiEndpoints.SendCommunityModmailMessage(idOrSlug, conversationId, body),
          cancellationToken).ConfigureAwait(false)).Message;

  public async Task<CommunityModmailThread> UpdateCommunityModmailThreadAsync(
      string idOrSlug,
      string conversationId,
      object body,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityModmailThreadResponse>(
          VouchaApiEndpoints.UpdateCommunityModmailThread(idOrSlug, conversationId, body),
          cancellationToken).ConfigureAwait(false)).Thread;

  public Task<CommunitySavedRepliesResponse> FetchCommunitySavedRepliesAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      FetchCommunitySavedRepliesPageAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunitySavedRepliesResponse> FetchCommunitySavedRepliesPageAsync(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunitySavedRepliesResponse>(
          VouchaApiEndpoints.CommunitySavedReplies(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunitySavedReplyResponse> CreateCommunitySavedReplyAsync(
      string idOrSlug,
      object body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunitySavedReplyResponse>(
          VouchaApiEndpoints.CreateCommunitySavedReply(idOrSlug, body),
          cancellationToken);

  public Task DeleteCommunitySavedReplyAsync(
      string idOrSlug,
      string replyId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteCommunitySavedReply(idOrSlug, replyId), cancellationToken);

}
