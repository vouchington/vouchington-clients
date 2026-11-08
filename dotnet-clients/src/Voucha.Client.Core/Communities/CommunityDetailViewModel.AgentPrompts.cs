using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private int agentPromptReadRevision;
  private string? agentPromptEndCursor;
  private bool isLoadingAgentPrompts;
  private bool agentPromptDisposed;
  public CommunityAgentPromptSlotInfo? AgentPromptSlots { get; private set; }

  public async Task LoadAgentPromptsAsync(CancellationToken cancellationToken = default)
  {
    var sessionRevision = Volatile.Read(ref agentPromptSessionRevision);
    if (agentPromptDisposed || !AcceptsAgentPromptSession(sessionRevision)) return;
    isLoadingAgentPrompts = true;
    InvalidateCommunityListRequests();
    var request = Interlocked.Increment(ref agentPromptReadRevision);
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    var owner = sessionStore?.Current.Identity?.Id;
    var section = SelectedSection;
    SetCommunityContinuation(agentPromptEndCursor);
    ApplyAgentPromptRows();
    try
    {
      var page = await service.FetchAgentPromptsPageAsync(community, null, 25, cancellationToken).ConfigureAwait(true);
      if (request != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)) return;
      ApplyAgentPromptPage(page, false);
      agentPromptRefreshError = null;
      NotifyAgentPromptRefresh();
      if (!isCreatingAgentPrompt) CompleteLoad();
    }
    catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException or InvalidOperationException) &&
        (request != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)))
    {
    }
    finally
    {
      if (request == agentPromptReadRevision)
      {
        isLoadingAgentPrompts = false;
        SettleAgentPromptCreation();
        OnPropertyChanged(nameof(CanLoadMoreCommunityList));
      }
    }
  }

  private async Task LoadMoreAgentPromptPageAsync(
      string after, int revision, int request, string community,
      CommunityDetailSurfaceSection section, string? threadId, CancellationToken cancellationToken)
  {
    var sessionRevision = Volatile.Read(ref agentPromptSessionRevision);
    if (!AcceptsAgentPromptSession(sessionRevision) || agentPromptDisposed) return;
    var owner = sessionStore?.Current.Identity?.Id;
    try
    {
      var page = await service.FetchAgentPromptsPageAsync(community, after, 25, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentCommunityPage(revision, request, community, section, threadId) ||
          !AcceptsAgentPromptSession(sessionRevision) || agentPromptDisposed || owner != sessionStore?.Current.Identity?.Id) return;
      ApplyAgentPromptPage(page, true);
    }
    catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException or InvalidOperationException) &&
        !AcceptsAgentPromptSession(sessionRevision))
    {
    }
  }

  private void ApplyAgentPromptPage(CommunityAgentPromptsResponse page, bool append)
  {
    AgentPrompts = AppendUnique(append ? AgentPrompts : [], page.CommunityAgentPrompts, prompt => prompt.Id);
    AgentPromptSlots = page.SlotInfo;
    ApplyAgentPromptRows();
    agentPromptEndCursor = page.PageInfo.HasNextPage ? page.PageInfo.EndCursor : null;
    SetCommunityContinuation(agentPromptEndCursor);
    OnPropertyChanged(nameof(AgentPrompts));
    OnPropertyChanged(nameof(AgentPromptSlots));
  }

  private void ApplyAgentPromptRows()
  {
    Moderation = AgentPrompts.Select(prompt => Summary(
        prompt.Id, UiText.Verbatim(prompt.AgentId),
        UiText.Localized(prompt.IsSlotAllocated
            ? UiMessageKey.NativeDotnetCsharpCommunitiesAllocated
            : UiMessageKey.NativeDotnetCsharpCommunitiesUnallocated),
        Verbatim(prompt.ModelName))).ToArray();
  }

  private bool IsCurrentAgentPromptContext(string community, long revision, string? owner, CommunityDetailSurfaceSection section, long sessionRevision) =>
      AcceptsAgentPromptSession(sessionRevision) && !agentPromptDisposed && community == communityIdOrSlug && revision == Volatile.Read(ref communityContextRevision) &&
      owner == sessionStore?.Current.Identity?.Id && section == SelectedSection;

  private void ResetAgentPromptContext(bool notify = true)
  {
    Interlocked.Increment(ref agentPromptReadRevision);
    agentPromptCreationOperation++;
    isCreatingAgentPrompt = false;
    agentPromptCreationSettled = false;
    isLoadingAgentPrompts = false;
    AgentPrompts = [];
    AgentPromptSlots = null;
    agentPromptEndCursor = null;
    agentPromptCreationError = null;
    agentPromptRefreshError = null;
    HasReachedAgentPromptLimit = false;
    if (!notify) return;
    OnPropertyChanged(nameof(AgentPrompts));
    OnPropertyChanged(nameof(AgentPromptSlots));
    NotifyAgentPromptCreation();
    NotifyAgentPromptRefresh();
  }

  private void OnAgentPromptSessionChanged()
  {
    if (SelectedSection != CommunityDetailSurfaceSection.AgentPrompts)
    {
      ResetAgentPromptContext(notify: false);
      return;
    }
    ReleaseAgentPromptMutationState();
    ResetAgentPromptContext();
    InvalidateCommunityListRequests();
    SetCommunityContinuation(null);
    Moderation = [];
  }

  private void ReleaseAgentPromptMutationState()
  {
    if (isCreatingAgentPrompt && agentPromptCreationContext == Volatile.Read(ref communityContextRevision)) CompleteLoad();
  }
}
