using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private int agentPromptReadRevision;
  private bool isLoadingAgentPrompts;
  private bool agentPromptDisposed;
  public CommunityAgentPromptSlotInfo? AgentPromptSlots { get; private set; }

  public async Task LoadAgentPromptsAsync(CancellationToken cancellationToken = default)
  {
    if (agentPromptDisposed) return;
    InvalidateCommunityListRequests();
    var request = Interlocked.Increment(ref agentPromptReadRevision);
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    var owner = sessionStore?.Current.Identity?.Id;
    var section = SelectedSection;
    SetCommunityContinuation(null);
    isLoadingAgentPrompts = true;
    try
    {
      var page = await service.FetchAgentPromptsPageAsync(community, null, 25, cancellationToken).ConfigureAwait(true);
      if (request != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)) return;
      ApplyAgentPromptPage(page, false);
      CompleteLoad();
    }
    catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException or InvalidOperationException) &&
        (request != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)))
    {
    }
    finally
    {
      if (request == agentPromptReadRevision) isLoadingAgentPrompts = false;
    }
  }

  private async Task LoadMoreAgentPromptPageAsync(
      string after, int revision, int request, string community,
      CommunityDetailSurfaceSection section, string? threadId, CancellationToken cancellationToken)
  {
    var owner = sessionStore?.Current.Identity?.Id;
    var page = await service.FetchAgentPromptsPageAsync(community, after, 25, cancellationToken).ConfigureAwait(true);
    if (!IsCurrentCommunityPage(revision, request, community, section, threadId) ||
        agentPromptDisposed || owner != sessionStore?.Current.Identity?.Id) return;
    ApplyAgentPromptPage(page, true);
  }

  private void ApplyAgentPromptPage(CommunityAgentPromptsResponse page, bool append)
  {
    AgentPrompts = AppendUnique(append ? AgentPrompts : [], page.CommunityAgentPrompts, prompt => prompt.Id);
    AgentPromptSlots = page.SlotInfo;
    Moderation = AgentPrompts.Select(prompt => Summary(
        prompt.Id, UiText.Verbatim(prompt.AgentId),
        UiText.Localized(prompt.IsSlotAllocated
            ? UiMessageKey.NativeDotnetCsharpCommunitiesAllocated
            : UiMessageKey.NativeDotnetCsharpCommunitiesUnallocated),
        Verbatim(prompt.ModelName))).ToArray();
    SetCommunityContinuation(page.PageInfo.HasNextPage ? page.PageInfo.EndCursor : null);
    OnPropertyChanged(nameof(AgentPrompts));
    OnPropertyChanged(nameof(AgentPromptSlots));
  }

  private bool IsCurrentAgentPromptContext(string community, long revision, string? owner, CommunityDetailSurfaceSection section) =>
      !agentPromptDisposed && community == communityIdOrSlug && revision == Volatile.Read(ref communityContextRevision) &&
      owner == sessionStore?.Current.Identity?.Id && section == SelectedSection;

  private void ResetAgentPromptContext()
  {
    Interlocked.Increment(ref agentPromptReadRevision);
    agentPromptCreationOperation++;
    isCreatingAgentPrompt = false;
    isLoadingAgentPrompts = false;
    AgentPrompts = [];
    AgentPromptSlots = null;
    agentPromptCreationError = null;
    HasReachedAgentPromptLimit = false;
    OnPropertyChanged(nameof(AgentPrompts));
    OnPropertyChanged(nameof(AgentPromptSlots));
    NotifyAgentPromptCreation();
  }

  private void OnAgentPromptSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    if (isCreatingAgentPrompt && SelectedSection == CommunityDetailSurfaceSection.AgentPrompts &&
        agentPromptCreationContext == Volatile.Read(ref communityContextRevision) && agentPromptCreationRead == agentPromptReadRevision)
      CompleteLoad();
    ResetAgentPromptContext();
    if (SelectedSection != CommunityDetailSurfaceSection.AgentPrompts) return;
    InvalidateCommunityListRequests();
    SetCommunityContinuation(null);
    Moderation = [];
  }
}
