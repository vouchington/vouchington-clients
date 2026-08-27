namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectConversationsResponse>(
          VouchaApiEndpoints.MyMessages(Require(request).After, request.Limit),
          cancellationToken);

  public Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectConversationResponse>(
          VouchaApiEndpoints.MyMessage(conversationId),
          cancellationToken);

  public Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectConversationResponse>(
          VouchaApiEndpoints.CreateMyMessages(new CreateDirectConversationBody(Require(request).UserIds)),
          cancellationToken);

  public Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectMessagesResponse>(
          VouchaApiEndpoints.MyMessageConversationMessages(
              Require(request).ConversationId,
              request.After,
              request.Limit),
          cancellationToken);

  public Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectMessageResponse>(
          VouchaApiEndpoints.CreateMyMessageMessage(
              Require(request).ConversationId,
              new SendDirectMessageBody(request.Text)),
          cancellationToken);

  public Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectMessageParticipantsResponse>(
          VouchaApiEndpoints.MyMessageConversationParticipants(conversationId),
          cancellationToken);

  public Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectMessageParticipantResponse>(
          VouchaApiEndpoints.AddMyMessageConversationParticipant(
              Require(request).ConversationId,
              new AddDirectConversationParticipantBody(request.UserId)),
          cancellationToken);

  public Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.RemoveMyMessageConversationParticipant(conversationId, userId),
          cancellationToken);

  public Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<DirectConversationPolicyResponse>(
          VouchaApiEndpoints.UpdateMyMessageConversationParticipantPolicy(
              Require(request).ConversationId,
              new UpdateDirectConversationParticipantPolicyBody(request.ParticipantAddPolicy)),
          cancellationToken);

  public Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<UsersSearchResponse>(
          VouchaApiEndpoints.SearchUsers(Require(request).Query, request.After, request.Limit),
          cancellationToken);
}
