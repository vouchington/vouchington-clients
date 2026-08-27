using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public interface IAgentConversationsService
{
  Task<AgentsResponse> FetchAgentsAsync(FetchAgentsRequest request, CancellationToken cancellationToken = default);

  Task<AgentDetailResponse> FetchAgentAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  Task<AgentConversationsResponse> FetchAgentConversationsAsync(
      FetchAgentConversationsRequest request,
      CancellationToken cancellationToken = default);

  Task<AgentConversationDetailResponse> FetchAgentConversationAsync(
      FetchAgentConversationRequest request,
      CancellationToken cancellationToken = default);
}
