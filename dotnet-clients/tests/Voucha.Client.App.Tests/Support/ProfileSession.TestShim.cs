using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private ISessionStore sessionStore = new AnonymousSessionStore();
  private readonly SessionChangedSubscription sessionChangedSubscription = new();
  private bool lastStaffViewer;
  private bool hasLoaded = true;
  private readonly string? idOrUsername = null;
  internal Task SessionRefreshTask { get; private set; } = Task.CompletedTask;

  internal void AttachMetricSession(ISessionStore store)
  {
    sessionStore = store;
    RefreshViewerState();
    sessionChangedSubscription.TryAttach(store, (_, _) => SessionRefreshTask = RefreshAfterSessionChangedAsync());
  }
}
