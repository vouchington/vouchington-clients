namespace Voucha.Client.Core.Navigation;

public static class NativeChatRoutePaths
{
  public static bool IsChatRootPath(string path) =>
      string.Equals(path, "/chat", StringComparison.OrdinalIgnoreCase);

  public static bool IsChatSupportRootPath(string path) =>
      string.Equals(path, "/chat/support", StringComparison.OrdinalIgnoreCase) ||
      string.Equals(path, "/chat/support/new", StringComparison.OrdinalIgnoreCase);

  public static bool TryGetChatConversationId(string path, out string conversationId)
  {
    ArgumentNullException.ThrowIfNull(path);
    conversationId = string.Empty;
    if (!path.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/chat/support", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var slashIndex = path.IndexOf('/', "/chat/".Length);
    conversationId = slashIndex < 0 ? path["/chat/".Length..] : path["/chat/".Length..slashIndex];
    return conversationId.Length > 0;
  }

  public static bool TryGetSupportThreadId(string path, out string threadId)
  {
    ArgumentNullException.ThrowIfNull(path);
    threadId = string.Empty;
    if (!path.StartsWith("/chat/support/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("/new", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    threadId = path["/chat/support/".Length..];
    return threadId.Length > 0;
  }
}
