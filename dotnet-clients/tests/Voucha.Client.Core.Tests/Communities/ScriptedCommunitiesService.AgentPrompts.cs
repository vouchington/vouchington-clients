using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.Core.Tests.Communities;

internal sealed partial class ScriptedCommunitiesService
{
  public Func<string, Task>? AgentPromptMutationHandler { get; set; }
  public Func<string?, Task<CommunityAgentPromptsResponse>>? AgentPromptPageHandler { get; set; }
  public Func<CommunityAgentPromptUpsertRequest, Task<CommunityAgentPrompt>>? AgentPromptCreateHandler { get; set; }
  public Func<CommunityAgentPromptUpdateRequest, Task<CommunityAgentPrompt>>? AgentPromptUpdateHandler { get; set; }

  public Task<CommunityAgentPromptsResponse> FetchAgentPromptsPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default)
  {
    ForwardPageRequests.Add((CommunityDetailSurfaceSection.AgentPrompts, after, limit));
    return AgentPromptPageHandler?.Invoke(after) ?? FetchAgentPromptsAsync(idOrSlug, cancellationToken);
  }
}
