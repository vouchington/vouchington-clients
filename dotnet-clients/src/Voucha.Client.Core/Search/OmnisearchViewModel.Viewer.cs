using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args) => DispatchViewerStateChanged();

  private void OnSessionChanged(object? sender, SessionChangedEventArgs args) => DispatchViewerStateChanged();

  private void DispatchViewerStateChanged()
  {
    if (syncContext is not null)
    {
      syncContext.Post(_ => NotifyViewerStateChanged(), null);
    }
    else
    {
      NotifyViewerStateChanged();
    }
  }

  private void NotifyViewerStateChanged()
  {
    OnPropertyChanged(nameof(CanVoteSelectedHostname));
    OnPropertyChanged(nameof(CanClearSelectedHostnameVote));
    OnPropertyChanged(nameof(CanUseSelectedHostnameActions));
    OnPropertyChanged(nameof(CanTriggerSelectedUrlCrawl));
    OnPropertyChanged(nameof(HasSelectedReportTarget));
    OnPropertyChanged(nameof(CanReportSelectedHostname));
  }
}
