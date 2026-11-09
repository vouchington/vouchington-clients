namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(Community));
    OnPropertyChanged(nameof(CommunityMetrics));
    OnPropertyChanged(nameof(ListItemCounts));
    OnPropertyChanged(nameof(Members));
    OnPropertyChanged(nameof(Posts));
    OnPropertyChanged(nameof(News));
    OnPropertyChanged(nameof(PinnedPosts));
    OnPropertyChanged(nameof(Applications));
    OnPropertyChanged(nameof(Invites));
    OnPropertyChanged(nameof(Moderation));
    OnPropertyChanged(nameof(AutomodFlagError));
    OnPropertyChanged(nameof(AutomodFlagNotice));
    NotifyAgentPromptCreation();
    NotifyAgentPromptRefresh();
  }

  public void Dispose()
  {
    agentPromptDisposed = true;
    ApplicationDetailContextInvalidated?.Invoke();
    ResetAgentPromptContext();
    InvalidateCommunityListRequests();
    SetCommunityContinuation(null);
    agentPromptSessionObserver?.Dispose();
    localeSubscription?.Dispose();
  }
}
