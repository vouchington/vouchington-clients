using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed partial class TagManagementPage
{
  private EventHandler<SessionChangedEventArgs> sessionChangedHandler = null!;
  private bool isSessionChangedAttached;
  private int votePolicyVersion;

  public int VotePolicyVersion => votePolicyVersion;

  private void InitializeVotePolicyBindings() =>
      sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() =>
      {
        votePolicyVersion++;
        OnPropertyChanged(nameof(VotePolicyVersion));
      });

  protected override void OnDisappearing()
  {
    DetachSessionChanged();
    base.OnDisappearing();
  }

  private void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  private void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }
}
