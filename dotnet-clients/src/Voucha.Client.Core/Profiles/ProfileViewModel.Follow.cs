using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private bool isFollowingUser;

  public bool IsFollowingUser
  {
    get => isFollowingUser;
    private set
    {
      if (SetProperty(ref isFollowingUser, value)) RefreshFollowActionProperties();
    }
  }

  public bool CanShowFollowAction { get; private set; }

  public bool IsSignedInViewer => !string.IsNullOrWhiteSpace(currentViewerUserId);

  public bool CanFollowUser => CanShowFollowAction && !IsBlockedUser && !IsFollowingUser;
  public bool CanUnfollowUser => CanShowFollowAction && !IsBlockedUser && IsFollowingUser;

  public async Task ToggleFollowUserAsync(CancellationToken cancellationToken = default)
  {
    if (IsBlockedUser) return;
    var wasFollowing = IsFollowingUser;
    await ToggleUserBookmarkAsync(BookmarkPredicate.Follow, !IsFollowingUser, cancellationToken).ConfigureAwait(true);
    if (!wasFollowing && IsFollowingUser)
      await LoadUserTrustContextAsync(cancellationToken).ConfigureAwait(true);
  }

  private void RefreshFollowActionProperties()
  {
    OnPropertyChanged(nameof(CanFollowUser));
    OnPropertyChanged(nameof(CanUnfollowUser));
  }
}
