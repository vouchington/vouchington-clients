using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public sealed class ApiChatService : IChatService
{
  private readonly VouchaApiClient client;

  public ApiChatService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<ChatConversationListResponse> FetchMyConversationsAsync(
      string? after = null,
      int limit = 50,
      CancellationToken cancellationToken = default) =>
      client.FetchMyChatConversationsAsync(after, limit, cancellationToken);

  public Task<ChatConversationResponse> CreateConversationAsync(
      CreateChatConversationBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateChatConversationAsync(body, cancellationToken);

  public Task<ChatMessagesResponse> FetchConversationMessagesAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      client.FetchMyChatConversationMessagesAsync(conversationId, cancellationToken);

  public Task<ChatMessagesResponse> FetchConversationMessagesPageAsync(
      string conversationId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      client.FetchMyChatConversationMessagesPageAsync(conversationId, after, limit, cancellationToken);

  public Task<ChatConversationResponse> UpdateConversationTitleAsync(
      string conversationId,
      string title,
      CancellationToken cancellationToken = default) =>
      client.UpdateChatConversationTitleAsync(conversationId, title, cancellationToken);

  public Task<ChatConversationResponse> GenerateConversationTitleAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      client.GenerateChatConversationTitleAsync(conversationId, cancellationToken);

  public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
      client.DeleteChatConversationAsync(conversationId, cancellationToken);

  public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(
      string conversationId,
      CreateClientGeneratedChatBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateClientGeneratedChatAsync(conversationId, body, cancellationToken);

}
