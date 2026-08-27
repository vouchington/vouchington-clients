using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;

namespace Voucha.Client.App.Pages;

public sealed class OmnisearchPageBinding : BindableObject
{
  private readonly OmnisearchViewModel viewModel;

  public OmnisearchPageBinding(
      OmnisearchViewModel viewModel,
      IReadOnlyList<NavigationGroupViewModel> linkGroups)
  {
    this.viewModel = viewModel;
    LinkGroups = linkGroups;
    CanBrowseUrls = LinkGroups
        .SelectMany(group => group.Items)
        .Any(item => item.Href == "/urls");
    viewModel.PropertyChanged += (_, e) =>
    {
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.Query))
      {
        OnPropertyChanged(nameof(Query));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.Groups))
      {
        OnPropertyChanged(nameof(Groups));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.ErrorMessage))
      {
        OnPropertyChanged(nameof(ErrorMessage));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.HasError))
      {
        OnPropertyChanged(nameof(HasError));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.HasSelectedHostname))
      {
        OnPropertyChanged(nameof(HasSelectedHostname));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.HasSelectedUrl))
      {
        OnPropertyChanged(nameof(HasSelectedUrl));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.CanUseSelectedHostnameActions))
      {
        OnPropertyChanged(nameof(CanUseSelectedHostnameActions));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.CanVoteSelectedHostname))
      {
        OnPropertyChanged(nameof(CanVoteSelectedHostname));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.CanClearSelectedHostnameVote))
      {
        OnPropertyChanged(nameof(CanClearSelectedHostnameVote));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.CanTriggerSelectedUrlCrawl))
      {
        OnPropertyChanged(nameof(CanTriggerSelectedUrlCrawl));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.SupportsWebSearchNavigation))
      {
        OnPropertyChanged(nameof(SupportsWebSearchNavigation));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.SelectedFediverseProvider))
      {
        OnPropertyChanged(nameof(SelectedFediverseProvider));
        OnPropertyChanged(nameof(IsAllProvidersSelected));
        OnPropertyChanged(nameof(IsPeerTubeSelected));
        OnPropertyChanged(nameof(IsMastodonSelected));
        OnPropertyChanged(nameof(IsLemmySelected));
        OnPropertyChanged(nameof(IsBlueskySelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.HasSelectedReportTarget))
      {
        OnPropertyChanged(nameof(HasSelectedReportTarget));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.CanReportSelectedHostname))
      {
        OnPropertyChanged(nameof(CanReportSelectedHostname));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(OmnisearchViewModel.ReportButtonText))
      {
        OnPropertyChanged(nameof(LocalizedReportButtonText));
      }
    };
  }

  public string Query
  {
    get => viewModel.Query;
    set => viewModel.Query = value;
  }

  public IReadOnlyList<OmnisearchResultGroup> Groups => viewModel.Groups;

  public string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasError => viewModel.HasError;

  public bool HasSelectedHostname => viewModel.HasSelectedHostname;

  public bool HasSelectedUrl => viewModel.HasSelectedUrl;

  public bool CanUseSelectedHostnameActions => viewModel.CanUseSelectedHostnameActions;

  public bool CanVoteSelectedHostname => viewModel.CanVoteSelectedHostname;

  public bool CanClearSelectedHostnameVote => viewModel.CanClearSelectedHostnameVote;

  public bool CanTriggerSelectedUrlCrawl => viewModel.CanTriggerSelectedUrlCrawl;

  public bool SupportsWebSearchNavigation => viewModel.SupportsWebSearchNavigation;

  public bool SupportsFediverseProviders => viewModel.SupportsFediverseProviders;

  public FediverseProvider SelectedFediverseProvider => viewModel.SelectedFediverseProvider;

  public bool IsAllProvidersSelected => SelectedFediverseProvider == FediverseProvider.All;
  public bool IsPeerTubeSelected => SelectedFediverseProvider == FediverseProvider.PeerTube;
  public bool IsMastodonSelected => SelectedFediverseProvider == FediverseProvider.Mastodon;
  public bool IsLemmySelected => SelectedFediverseProvider == FediverseProvider.Lemmy;
  public bool IsBlueskySelected => SelectedFediverseProvider == FediverseProvider.Bluesky;

  public bool HasSelectedReportTarget => viewModel.HasSelectedReportTarget;

  public bool CanReportSelectedHostname => viewModel.CanReportSelectedHostname;

  public string LocalizedReportButtonText => UiCopy.Resolve(viewModel.ReportButtonText);

  public IReadOnlyList<NavigationGroupViewModel> LinkGroups { get; }

  public bool HasLinkGroups => LinkGroups.Count > 0;

  public bool CanBrowseUrls { get; }
}
