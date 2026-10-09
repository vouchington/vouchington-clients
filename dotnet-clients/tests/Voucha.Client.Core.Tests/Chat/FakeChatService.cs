using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.Core.Tests.Chat;

internal sealed partial class FakeChatService : IChatService
{
  public ChatConversationListResponse ConversationsResult { get; set; } =
      new([], new PageInfo(null, false, null));

  public ChatConversationResponse CreateConversationResult { get; set; } =
      new(new ChatConversation("conversation-1", "", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow, null, null, null));

  public Func<CreateChatConversationBody, CancellationToken, Task<ChatConversationResponse>>? CreateConversationAsyncOverride { get; set; }

  public int CreateConversationCount { get; private set; }

  public ChatConversationResponse UpdateConversationTitleResult { get; set; } =
      new(new ChatConversation("conversation-1", "Updated", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow, null, null, null));

  public Exception? UpdateConversationTitleError { get; set; }

  public Exception? CreateConversationError { get; set; }

  public Exception? FetchMyConversationsError { get; set; }

  public Func<string?, int, CancellationToken, Task<ChatConversationListResponse>>? FetchMyConversationsAsyncOverride { get; set; }

  public int FetchMyConversationsCount { get; private set; }

  public Exception? DeleteConversationError { get; set; }


  public string? LastChatPath { get; private set; }

  public CreateClientGeneratedChatBody? LastClientGeneratedChatBody { get; private set; }


  public Func<string, CreateClientGeneratedChatBody, CancellationToken, Task<ClientGeneratedChatResponse>>? CreateClientGeneratedChatAsyncOverride { get; set; }


  public Task<ChatConversationListResponse> FetchMyConversationsAsync(
      string? after = null,
      int limit = 50,
      CancellationToken cancellationToken = default)
  {
    if (FetchMyConversationsError is not null) throw FetchMyConversationsError;
    if (FetchMyConversationsAsyncOverride is not null)
    {
      FetchMyConversationsCount += 1;
      return FetchMyConversationsAsyncOverride(after, limit, cancellationToken);
    }

    return Task.FromResult(ConversationsResult);
  }

  public Task<ChatConversationResponse> CreateConversationAsync(
      CreateChatConversationBody body,
      CancellationToken cancellationToken = default)
  {
    CreateConversationCount++;
    if (CreateConversationError is not null) throw CreateConversationError;
    if (CreateConversationAsyncOverride is not null)
    {
      return CreateConversationAsyncOverride(body, cancellationToken);
    }

    return Task.FromResult(CreateConversationResult);
  }

  public Task<ChatConversationResponse> UpdateConversationTitleAsync(
      string conversationId,
      string title,
      CancellationToken cancellationToken = default)
  {
    if (UpdateConversationTitleError is not null) throw UpdateConversationTitleError;
    return Task.FromResult(UpdateConversationTitleResult);
  }

  public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
  {
    if (DeleteConversationError is not null) throw DeleteConversationError;
    return Task.CompletedTask;
  }

  public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(
      string conversationId,
      CreateClientGeneratedChatBody body,
      CancellationToken cancellationToken = default)
  {
    LastChatPath = $"/api/v1/conversations/{conversationId}/client-generated-chat";
    LastClientGeneratedChatBody = body;
    if (CreateClientGeneratedChatAsyncOverride is not null)
    {
      return CreateClientGeneratedChatAsyncOverride(conversationId, body, cancellationToken);
    }

    return Task.FromResult(new ClientGeneratedChatResponse(
        new ChatMessage(
            "message-user",
            conversationId,
            DateTimeOffset.UtcNow,
            "user-1",
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            new ChatMessageContent("user", body.Message, null)),
        new ChatMessage(
            "message-assistant",
            conversationId,
            DateTimeOffset.UtcNow,
            "assistant-1",
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            new ChatMessageContent("assistant", body.AssistantContent, null)),
        new ClientGeneratedChatTurn("message-user", "message-assistant")));
  }

}
