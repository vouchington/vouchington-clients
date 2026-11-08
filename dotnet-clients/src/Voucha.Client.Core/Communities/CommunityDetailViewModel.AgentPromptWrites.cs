namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private bool isWritingAgentPrompt;
  private bool agentPromptWriteSettled;
  private long agentPromptWriteOperation;
  private long agentPromptWriteContext;
  private long agentPromptWriteSession;

  private async Task<bool> MutateAgentPromptAsync(Func<Task> mutation, CancellationToken cancellationToken)
  {
    var session = Volatile.Read(ref agentPromptSessionRevision);
    if (!AcceptsAgentPromptSession(session) || agentPromptDisposed || IsLoading || isCreatingAgentPrompt ||
        isWritingAgentPrompt || isLoadingAgentPrompts || isLoadingMoreCommunityList || string.IsNullOrWhiteSpace(communityIdOrSlug)) return false;
    var community = communityIdOrSlug;
    var context = Volatile.Read(ref communityContextRevision);
    var owner = sessionStore?.Current.Identity?.Id;
    var section = SelectedSection;
    var operation = ++agentPromptWriteOperation;
    isWritingAgentPrompt = true;
    agentPromptWriteSettled = false;
    agentPromptWriteContext = context;
    agentPromptWriteSession = session;
    BeginMutation();
    bool IsCurrent() => operation == agentPromptWriteOperation && IsCurrentAgentPromptContext(community, context, owner, section, session);
    try
    {
      await mutation().ConfigureAwait(true);
      if (!IsCurrent()) return false;
      await LoadSelectedSectionAsync(cancellationToken).ConfigureAwait(true);
      if (!IsCurrent()) return false;
      if (!isLoadingAgentPrompts) CompleteLoad();
      return !HasError;
    }
    catch (OperationCanceledException)
    {
      if (IsCurrent() && !isLoadingAgentPrompts) CompleteLoad();
      return false;
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      if (IsCurrent()) HandleLoadException(ex);
      return false;
    }
    finally
    {
      if (operation == agentPromptWriteOperation && AcceptsAgentPromptSession(session))
      {
        agentPromptWriteSettled = true;
        SettleAgentPromptWrite();
      }
    }
  }

  private void SettleAgentPromptWrite()
  {
    if (!isWritingAgentPrompt || !agentPromptWriteSettled || isLoadingAgentPrompts ||
        !AcceptsAgentPromptSession(agentPromptWriteSession)) return;
    isWritingAgentPrompt = false;
    if (agentPromptWriteContext == Volatile.Read(ref communityContextRevision) && IsLoading) CompleteLoad();
  }

  private void ResetAgentPromptWrite()
  {
    agentPromptWriteOperation++;
    isWritingAgentPrompt = false;
    agentPromptWriteSettled = false;
  }
}
