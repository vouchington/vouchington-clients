namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private string? currentViewerUserId;
  private string? currentViewerUsername;
  private bool currentViewerCanCastPublicVotes = true;
  private bool currentViewerCanCreateUserTagVotes = true;

  public void SetCurrentViewer(
      string? userId,
      string? username,
      bool canCastPublicVotes = true,
      bool? canCreateUserTagVotes = null)
  {
    currentViewerUserId = userId;
    currentViewerUsername = username;
    currentViewerCanCastPublicVotes = canCastPublicVotes;
    currentViewerCanCreateUserTagVotes = canCreateUserTagVotes ?? canCastPublicVotes;
    RefreshProfileActionEligibility();
    OnPropertyChanged(nameof(CanVoteUserTrust));
    OnPropertyChanged(nameof(CanCreateUserTrustVote));
    OnPropertyChanged(nameof(CanClearUserTrustVote));
    OnPropertyChanged(nameof(CanCreateUserTagVotes));
    OnPropertyChanged(nameof(CanClearUserTagVotes));
  }

  private void RefreshProfileActionEligibility()
  {
    var isSignedIn = !string.IsNullOrWhiteSpace(currentViewerUserId);
    var isSelf = string.Equals(User?.Id, currentViewerUserId, StringComparison.Ordinal) ||
        (!string.IsNullOrWhiteSpace(User?.Username) &&
          string.Equals(User?.Username, currentViewerUsername, StringComparison.OrdinalIgnoreCase));
    CanActOnUser = !CanEdit && isSignedIn && !string.IsNullOrEmpty(User?.Id) && !isSelf;
    CanShowFollowAction = !CanEdit && !string.IsNullOrEmpty(User?.Id) && !isSelf;
    OnPropertyChanged(nameof(CanShowFollowAction));
    RefreshFollowActionProperties();
    OnPropertyChanged(nameof(IsSignedInViewer));
  }
}
