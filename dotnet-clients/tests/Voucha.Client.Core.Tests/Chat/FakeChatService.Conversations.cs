using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.Core.Tests.Chat;

internal sealed partial class FakeChatService
{
  public ChatMessagesResponse ConversationMessagesResult { get; set; } =
      new([], new PageInfo(null, false, null));

  public Exception? FetchConversationMessagesError { get; set; }

  public int FetchConversationMessagesCount { get; private set; }

  public Func<string, CancellationToken, Task<ChatMessagesResponse>>? FetchConversationMessagesAsyncOverride { get; set; }

  public Func<string, string?, int?, CancellationToken, Task<ChatMessagesResponse>>? FetchConversationMessagesPageAsyncOverride { get; set; }

  public int FetchConversationMessagesPageCount { get; private set; }

  public Func<string, CancellationToken, Task<ChatConversationResponse>>? GenerateConversationTitleAsyncOverride { get; set; }

  public int GenerateConversationTitleCount { get; private set; }

  public ChatConversationResponse GenerateConversationTitleResult { get; set; } =
      new(new ChatConversation("conversation-1", "Generated", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow, null, null, null));

  public Task<ChatMessagesResponse> FetchConversationMessagesAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    FetchConversationMessagesCount += 1;
    if (FetchConversationMessagesError is not null) throw FetchConversationMessagesError;
    if (FetchConversationMessagesAsyncOverride is not null)
    {
      return FetchConversationMessagesAsyncOverride(conversationId, cancellationToken);
    }

    return Task.FromResult(ConversationMessagesResult);
  }

  public Task<ChatMessagesResponse> FetchConversationMessagesPageAsync(
      string conversationId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default)
  {
    FetchConversationMessagesPageCount += 1;
    if (FetchConversationMessagesPageAsyncOverride is not null)
    {
      return FetchConversationMessagesPageAsyncOverride(conversationId, after, limit, cancellationToken);
    }

    return FetchConversationMessagesAsync(conversationId, cancellationToken);
  }

  public Task<ChatConversationResponse> GenerateConversationTitleAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    GenerateConversationTitleCount += 1;
    if (GenerateConversationTitleAsyncOverride is not null)
    {
      return GenerateConversationTitleAsyncOverride(conversationId, cancellationToken);
    }

    return Task.FromResult(GenerateConversationTitleResult);
  }
}
