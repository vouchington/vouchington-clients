using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args)
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
