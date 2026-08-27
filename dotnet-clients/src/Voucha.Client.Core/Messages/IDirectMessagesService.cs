using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Messages;

public interface IDirectMessagesService
{
  Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default);

  Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default);

  Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default);

  Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default);

  Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default);

  Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default);

  Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default);

  Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default);

  Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default);

  Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default);
}
