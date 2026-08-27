using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public interface IChatService
{
  Task<ChatConversationListResponse> FetchMyConversationsAsync(
      string? after = null,
      int limit = 50,
      CancellationToken cancellationToken = default);

  Task<ChatConversationResponse> CreateConversationAsync(
      CreateChatConversationBody body,
      CancellationToken cancellationToken = default);

  Task<ChatMessagesResponse> FetchConversationMessagesAsync(
      string conversationId,
      CancellationToken cancellationToken = default);

  Task<ChatMessagesResponse> FetchConversationMessagesPageAsync(
      string conversationId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      FetchConversationMessagesAsync(conversationId, cancellationToken);

  Task<ChatConversationResponse> UpdateConversationTitleAsync(
      string conversationId,
      string title,
      CancellationToken cancellationToken = default);

  Task<ChatConversationResponse> GenerateConversationTitleAsync(
      string conversationId,
      CancellationToken cancellationToken = default);

  Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);

  IAsyncEnumerable<ChatStreamEvent> StreamConversationAsync(
      string conversationId,
      string message,
      string? provider = null,
      CancellationToken cancellationToken = default);

  Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(
      string conversationId,
      CreateClientGeneratedChatBody body,
      CancellationToken cancellationToken = default);

  Task<SupportThreadListResponse> FetchSupportThreadsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<CreateSupportThreadResponse> CreateSupportThreadAsync(
      CreateSupportThreadBody body,
      CancellationToken cancellationToken = default);

  Task<SupportThreadDetailResponse> FetchSupportThreadAsync(
      string threadId,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);
}
