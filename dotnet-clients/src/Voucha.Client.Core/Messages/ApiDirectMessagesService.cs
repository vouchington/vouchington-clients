using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Messages;

public sealed class ApiDirectMessagesService : IDirectMessagesService
{
  private readonly VouchaApiClient client;

  public ApiDirectMessagesService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchDirectMessagesAsync(request, cancellationToken);

  public Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      client.FetchDirectConversationAsync(conversationId, cancellationToken);

  public Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default) =>
      client.CreateDirectConversationAsync(request, cancellationToken);

  public Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchDirectConversationMessagesAsync(request, cancellationToken);

  public Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default) =>
      client.SendDirectMessageAsync(request, cancellationToken);

  public Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      client.FetchDirectConversationParticipantsAsync(conversationId, cancellationToken);

  public Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default) =>
      client.AddDirectConversationParticipantAsync(request, cancellationToken);

  public Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default) =>
      client.RemoveDirectConversationParticipantAsync(conversationId, userId, cancellationToken);

  public Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default) =>
      client.UpdateDirectConversationParticipantPolicyAsync(request, cancellationToken);

  public Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default) =>
      client.SearchUsersAsync(request, cancellationToken);
}
