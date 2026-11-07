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
  private int agentPromptCreationRead;
  public bool HasReachedAgentPromptLimit { get; private set; }
  public string? AgentPromptCreationError => agentPromptCreationError is { } text ? localization.Resolve(text) : null;

  public async Task<bool> CreateAgentPromptAsync(
      string prompt, string? modelName = null, string? modelProvider = null, CancellationToken cancellationToken = default)
  {
    if (!CanModerateCommunity || isCreatingAgentPrompt || IsLoading || isLoadingAgentPrompts || isLoadingMoreCommunityList || agentPromptDisposed) return false;
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
    agentPromptCreationRead = readRevision;
    BeginMutation();
    try
    {
      await service.CreateAgentPromptAsync(community,
          new CommunityAgentPromptUpsertRequest(prompt, modelName, modelProvider), cancellationToken).ConfigureAwait(true);
      if (readRevision != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)) return false;
      var reload = LoadAgentPromptsAsync(cancellationToken);
      readRevision = agentPromptReadRevision;
      agentPromptCreationRead = readRevision;
      await reload.ConfigureAwait(true);
      if (readRevision != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)) return false;
      CompleteLoad();
      return true;
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict && ex.ErrorCode == "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED")
    {
      if (readRevision != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)) return false;
      HasReachedAgentPromptLimit = true;
      agentPromptCreationError = string.IsNullOrWhiteSpace(ex.ApiMessage)
          ? UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed)
          : UiText.UserContent(ex.ApiMessage);
      CompleteLoad();
      NotifyAgentPromptCreation();
      return false;
    }
    catch (OperationCanceledException)
    {
      if (readRevision == agentPromptReadRevision && IsCurrentAgentPromptContext(community, revision, owner, section)) CompleteLoad();
      return false;
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      if (readRevision == agentPromptReadRevision && IsCurrentAgentPromptContext(community, revision, owner, section)) HandleLoadException(ex);
      return false;
    }
    finally
    {
      if (operation == agentPromptCreationOperation) isCreatingAgentPrompt = false;
    }
  }

  private void NotifyAgentPromptCreation()
  {
    OnPropertyChanged(nameof(HasReachedAgentPromptLimit));
    OnPropertyChanged(nameof(AgentPromptCreationError));
  }
}
