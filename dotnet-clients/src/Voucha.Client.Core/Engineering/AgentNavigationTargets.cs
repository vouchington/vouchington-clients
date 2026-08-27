using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Engineering;

public static class AgentNavigationTargets
{
  public static NativeRoutePath Detail(string agentIdOrSlug, AgentConversationFilter? filter = null)
  {
    var target = NativeRoutePath.Segments("agent", agentIdOrSlug);
    return filter is null ? target : target.WithQuery(filter.QueryName, filter.Value);
  }

  public static NativeRoutePath Conversation(string agentIdOrSlug, string conversationId) =>
      NativeRoutePath.Segments("agent", agentIdOrSlug, "conversation", conversationId);
}
