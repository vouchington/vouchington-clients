namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityAiAgentsResponse> FetchCommunityAiAgentsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAiAgentsResponse>(VouchaApiEndpoints.CommunityAiAgents(idOrSlug), cancellationToken);

  public Task EnableCommunityAiAgentAsync(
      string idOrSlug,
      string agentSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.EnableCommunityAiAgent(idOrSlug, agentSlug), cancellationToken);

  public Task DeleteCommunityAiAgentAsync(
      string idOrSlug,
      string agentSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteCommunityAiAgent(idOrSlug, agentSlug), cancellationToken);

  public Task<CommunityAgentPromptsResponse> FetchCommunityAgentPromptsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAgentPromptsResponse>(
          VouchaApiEndpoints.CommunityAgentPrompts(idOrSlug),
          cancellationToken);

  public async Task<CommunityAgentPrompt> FetchCommunityAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityAgentPromptResponse>(
          VouchaApiEndpoints.CommunityAgentPrompt(idOrSlug, promptId),
          cancellationToken).ConfigureAwait(false)).CommunityAgentPrompt;

  public async Task<CommunityAgentPrompt> CreateCommunityAgentPromptAsync(
      string idOrSlug,
      CommunityAgentPromptUpsertRequest request,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityAgentPromptResponse>(
          VouchaApiEndpoints.CreateCommunityAgentPrompt(idOrSlug, Require(request)),
          cancellationToken).ConfigureAwait(false)).CommunityAgentPrompt;

  public async Task<CommunityAgentPrompt> UpdateCommunityAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptUpdateRequest request,
      CancellationToken cancellationToken = default) =>
      (await SendAsync<CommunityAgentPromptResponse>(
          VouchaApiEndpoints.UpdateCommunityAgentPrompt(idOrSlug, promptId, Require(request)),
          cancellationToken).ConfigureAwait(false)).CommunityAgentPrompt;

  public Task DeleteCommunityAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteCommunityAgentPrompt(idOrSlug, promptId), cancellationToken);

  public Task AllocateCommunityAgentPromptSlotAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.AllocateCommunityAgentPromptSlot(idOrSlug, promptId), cancellationToken);

  public Task DeallocateCommunityAgentPromptSlotAsync(
      string idOrSlug,
      string promptId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeallocateCommunityAgentPromptSlot(idOrSlug, promptId), cancellationToken);

  public Task<System.Text.Json.JsonElement> TestCommunityAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptTestRunRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<System.Text.Json.JsonElement>(
          VouchaApiEndpoints.TestCommunityAgentPrompt(idOrSlug, promptId, Require(request)),
          cancellationToken);

  public Task<CommunityAgentPromptHistoryResponse> FetchCommunityAgentPromptHistoryAsync(
      string idOrSlug,
      string? promptId = null,
      string? before = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAgentPromptHistoryResponse>(
          VouchaApiEndpoints.CommunityAgentPromptHistory(idOrSlug, promptId, before),
          cancellationToken);

  public Task<CommunityAutomodFeedbackResponse> RecordCommunityAutomodFeedbackAsync(
      string idOrSlug,
      string sourceKey,
      CommunityAutomodFeedbackRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAutomodFeedbackResponse>(
          VouchaApiEndpoints.RecordCommunityAutomodFeedback(idOrSlug, sourceKey, Require(request)),
          cancellationToken);

  public Task<CommunityAutomodSimulation> SimulateCommunityAutomodAsync(
      string idOrSlug,
      CommunityAutomodSimulationRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAutomodSimulation>(
          VouchaApiEndpoints.SimulateCommunityAutomod(idOrSlug, Require(request)),
          cancellationToken);
}
