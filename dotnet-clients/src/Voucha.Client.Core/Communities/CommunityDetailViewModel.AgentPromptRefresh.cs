using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private UiText? agentPromptRefreshError;
  public bool HasAgentPromptRefreshError => agentPromptRefreshError is not null;
  public string? AgentPromptRefreshError => agentPromptRefreshError is { } text ? localization.Resolve(text) : null;

  public Task<bool> RetryAgentPromptRefreshAsync(CancellationToken cancellationToken = default) =>
      IsLoading || isLoadingAgentPrompts || agentPromptDisposed
          ? Task.FromResult(false) : RefreshAgentPromptsAfterCreateAsync(cancellationToken);

  private async Task<bool> RefreshAgentPromptsAfterCreateAsync(CancellationToken cancellationToken)
  {
    if (agentPromptDisposed) return false;
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityContextRevision);
    var owner = sessionStore?.Current.Identity?.Id;
    var section = SelectedSection;
    agentPromptRefreshError = null;
    NotifyAgentPromptRefresh();
    var refresh = LoadAgentPromptsAsync(cancellationToken);
    var readRevision = agentPromptReadRevision;
    try
    {
      await refresh.ConfigureAwait(true);
      return readRevision == agentPromptReadRevision && IsCurrentAgentPromptContext(community, revision, owner, section);
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
    {
      if (readRevision != agentPromptReadRevision || !IsCurrentAgentPromptContext(community, revision, owner, section)) return false;
      agentPromptRefreshError = ex is VouchaApiException api && !string.IsNullOrWhiteSpace(api.ApiMessage)
          ? UiText.UserContent(api.ApiMessage) : UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed);
      NotifyAgentPromptRefresh();
      return false;
    }
  }

  private void NotifyAgentPromptRefresh()
  {
    OnPropertyChanged(nameof(HasAgentPromptRefreshError));
    OnPropertyChanged(nameof(AgentPromptRefreshError));
  }
}
