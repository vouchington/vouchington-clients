using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeChatRoutePathsTests
{
  [Theory]
  [InlineData("/chat/support")]
  [InlineData("/chat/support/new")]
  [InlineData("/chat/support/thread-1")]
  [InlineData("/CHAT/SUPPORT/new")]
  public void RetiredSupportLinksDoNotResolveAsConversations(string path)
  {
    Assert.False(NativeChatRoutePaths.TryGetChatConversationId(path, out var conversationId));
    Assert.Equal(string.Empty, conversationId);
  }

  [Theory]
  [InlineData("/chat/conversation-1", "conversation-1")]
  [InlineData("/chat/supportive", "supportive")]
  public void OrdinaryChatLinksStillResolveAsConversations(string path, string expectedId)
  {
    Assert.True(NativeChatRoutePaths.TryGetChatConversationId(path, out var conversationId));
    Assert.Equal(expectedId, conversationId);
  }
}
