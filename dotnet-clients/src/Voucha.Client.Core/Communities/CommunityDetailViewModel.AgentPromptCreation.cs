using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private UiText? agentPromptCreationError;
  private long agentPromptCreationOperation;
  private bool isCreatingAgentPrompt;
  private long agentPromptCreationContext;
  private long agentPromptCreationSession;
  private bool agentPromptCreationSettled;
  public bool HasReachedAgentPromptLimit { get; private set; }
  public string? AgentPromptCreationError => agentPromptCreationError is { } text ? localization.Resolve(text) : null;

  public async Task<bool> CreateAgentPromptAsync(
      string prompt, string? modelName = null, string? modelProvider = null, CancellationToken cancellationToken = default)
  {
    var sessionRevision = Volatile.Read(ref agentPromptSessionRevision);
    if (!AcceptsAgentPromptSession(sessionRevision) || !CanModerateCommunity || isCreatingAgentPrompt || isWritingAgentPrompt || IsLoading || isLoadingAgentPrompts || isLoadingMoreCommunityList || agentPromptDisposed) return false;
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    var readRevision = agentPromptReadRevision;
    var owner = sessionStore?.Current.Identity?.Id;
    var section = SelectedSection;
    agentPromptCreationError = null;
    HasReachedAgentPromptLimit = false;
    NotifyAgentPromptCreation();
    var operation = ++agentPromptCreationOperation;
    isCreatingAgentPrompt = true;
    agentPromptCreationContext = revision;
    agentPromptCreationSession = sessionRevision;
    agentPromptCreationSettled = false;
    BeginMutation();
    try
    {
      await service.CreateAgentPromptAsync(community,
          new CommunityAgentPromptUpsertRequest(prompt, modelName, modelProvider), cancellationToken).ConfigureAwait(true);
      if (!IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)) return false;
      await RefreshAgentPromptsAfterCreateAsync(cancellationToken).ConfigureAwait(true);
      var current = IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision);
      if (current && !isLoadingAgentPrompts) CompleteLoad();
      return current;
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict && ex.ErrorCode == "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED")
    {
      if (operation != agentPromptCreationOperation || !IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)) return false;
      HasReachedAgentPromptLimit = true;
      agentPromptCreationError = string.IsNullOrWhiteSpace(ex.ApiMessage)
          ? UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed)
          : UiText.UserContent(ex.ApiMessage);
      if (!isLoadingAgentPrompts) CompleteLoad();
      NotifyAgentPromptCreation();
      return false;
    }
    catch (OperationCanceledException)
    {
      if (readRevision == agentPromptReadRevision && IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)) CompleteLoad();
      return false;
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      if (operation == agentPromptCreationOperation && IsCurrentAgentPromptContext(community, revision, owner, section, sessionRevision)) HandleLoadException(ex);
      return false;
    }
    finally
    {
      if (operation == agentPromptCreationOperation && AcceptsAgentPromptSession(sessionRevision))
      {
        agentPromptCreationSettled = true;
        SettleAgentPromptCreation();
      }
    }
  }

  private void SettleAgentPromptCreation()
  {
    if (!isCreatingAgentPrompt || !agentPromptCreationSettled || isLoadingAgentPrompts ||
        !AcceptsAgentPromptSession(agentPromptCreationSession)) return;
    isCreatingAgentPrompt = false;
    if (agentPromptCreationContext == Volatile.Read(ref communityContextRevision) && IsLoading) CompleteLoad();
  }

  private void NotifyAgentPromptCreation()
  {
    OnPropertyChanged(nameof(HasReachedAgentPromptLimit));
    OnPropertyChanged(nameof(AgentPromptCreationError));
  }
}
