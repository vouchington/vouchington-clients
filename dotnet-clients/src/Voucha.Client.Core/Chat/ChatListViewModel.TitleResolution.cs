using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatListViewModel
{
  private const int ConversationLookupPageSize = 50;

  public static async Task<string?> ResolveConversationTitleAsync(
      IChatService chatService,
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(chatService);
    ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

    try
    {
      string? after = null;
      while (true)
      {
        var response = await chatService.FetchMyConversationsAsync(after, ConversationLookupPageSize, cancellationToken)
            .ConfigureAwait(true);
        var match = response.Results.FirstOrDefault(row => row.Id == conversationId);
        if (match is not null) return match.Title;

        var nextCursor = response.PageInfo.EndCursor;
        if (!(response.PageInfo.HasNextPage || response.PageInfo.HasMore == true) ||
            nextCursor is not { Length: > 0 } ||
            nextCursor == after)
        {
          break;
        }

        after = nextCursor;
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      return null;
    }

    return null;
  }
}
