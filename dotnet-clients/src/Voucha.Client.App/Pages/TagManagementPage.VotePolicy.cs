using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed partial class TagManagementPage
{
  private EventHandler<SessionChangedEventArgs> sessionChangedHandler = null!;
  private readonly SessionChangedSubscription sessionChangedSubscription = new();
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

  private void AttachSessionChanged() =>
      sessionChangedSubscription.TryAttach(sessionStore, sessionChangedHandler);

  private void DetachSessionChanged() =>
      sessionChangedSubscription.TryDetach(sessionStore, sessionChangedHandler);
}
