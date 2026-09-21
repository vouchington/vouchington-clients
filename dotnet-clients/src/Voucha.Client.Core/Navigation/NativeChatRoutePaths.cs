namespace Voucha.Client.Core.Navigation;

public static class NativeChatRoutePaths
{
  public static bool IsChatRootPath(string path) =>
      string.Equals(path, "/chat", StringComparison.OrdinalIgnoreCase);

  public static bool TryGetChatConversationId(string path, out string conversationId)
  {
    ArgumentNullException.ThrowIfNull(path);
    conversationId = string.Empty;
    if (!path.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var slashIndex = path.IndexOf('/', "/chat/".Length);
    conversationId = slashIndex < 0 ? path["/chat/".Length..] : path["/chat/".Length..slashIndex];
    return conversationId.Length > 0;
  }

}
