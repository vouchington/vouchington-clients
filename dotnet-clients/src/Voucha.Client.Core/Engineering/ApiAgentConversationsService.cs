using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public sealed class ApiAgentConversationsService : IAgentConversationsService
{
  private readonly VouchaApiClient client;

  public ApiAgentConversationsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<AgentsResponse> FetchAgentsAsync(
      FetchAgentsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchAgentsAsync(request, cancellationToken);

  public Task<AgentDetailResponse> FetchAgentAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchAgentAsync(idOrSlug, cancellationToken);

  public Task<AgentConversationsResponse> FetchAgentConversationsAsync(
      FetchAgentConversationsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchAgentConversationsAsync(request, cancellationToken);

  public Task<AgentConversationDetailResponse> FetchAgentConversationAsync(
      FetchAgentConversationRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchAgentConversationAsync(request, cancellationToken);
}
