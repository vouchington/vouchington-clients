namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public void OnUiLocaleChanged()
  {
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
  }

  public void Dispose() => localeSubscription?.Dispose();
}
