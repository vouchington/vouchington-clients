namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<AgentsResponse> FetchAgentsAsync(
      FetchAgentsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<AgentsResponse>(
          VouchaApiEndpoints.Agents(Require(request).After, request.Limit), cancellationToken);

  public Task<AgentDetailResponse> FetchAgentAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      SendAsync<AgentDetailResponse>(VouchaApiEndpoints.Agent(idOrSlug), cancellationToken);

  public Task<AgentConversationsResponse> FetchAgentConversationsAsync(
      FetchAgentConversationsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<AgentConversationsResponse>(
          VouchaApiEndpoints.AgentConversations(
              Require(request).AgentIdOrSlug, request.After, request.Limit, request.Filter),
          cancellationToken);

  public Task<AgentConversationDetailResponse> FetchAgentConversationAsync(
      FetchAgentConversationRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<AgentConversationDetailResponse>(
          VouchaApiEndpoints.AgentConversation(
              Require(request).AgentIdOrSlug,
              request.ConversationId,
              request.After,
              request.Limit),
          cancellationToken);
}
