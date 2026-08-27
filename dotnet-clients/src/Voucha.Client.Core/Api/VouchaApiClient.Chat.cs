namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ChatConversationListResponse> FetchMyChatConversationsAsync(
      string? after = null,
      int limit = 50,
      CancellationToken cancellationToken = default) =>
      SendAsync<ChatConversationListResponse>(
          VouchaApiEndpoints.MyChatConversations(after, limit),
          cancellationToken);

  public Task<ChatConversationResponse> CreateChatConversationAsync(
      CreateChatConversationBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ChatConversationResponse>(
          VouchaApiEndpoints.CreateChatConversation(Require(body)),
          cancellationToken);

  public Task<ChatMessagesResponse> FetchMyChatConversationMessagesAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      FetchMyChatConversationMessagesPageAsync(conversationId, cancellationToken: cancellationToken);

  public Task<ChatMessagesResponse> FetchMyChatConversationMessagesPageAsync(
      string conversationId,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ChatMessagesResponse>(
          VouchaApiEndpoints.MyChatConversationMessages(conversationId, after, limit),
          cancellationToken);

  public Task<ChatConversationResponse> UpdateChatConversationTitleAsync(
      string conversationId,
      string title,
      CancellationToken cancellationToken = default) =>
      SendAsync<ChatConversationResponse>(
          VouchaApiEndpoints.UpdateChatConversationTitle(conversationId, new UpdateChatConversationTitleBody(title)),
          cancellationToken);

  public Task<ChatConversationResponse> GenerateChatConversationTitleAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ChatConversationResponse>(
          VouchaApiEndpoints.GenerateChatConversationTitle(conversationId),
          cancellationToken);

  public Task DeleteChatConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteChatConversation(conversationId), cancellationToken);

  public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(
      string conversationId,
      CreateClientGeneratedChatBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ClientGeneratedChatResponse>(
          VouchaApiEndpoints.CreateClientGeneratedChat(conversationId, Require(body)),
          cancellationToken);
}
