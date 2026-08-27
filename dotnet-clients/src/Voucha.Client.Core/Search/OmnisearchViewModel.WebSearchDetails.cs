namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public async Task LoadDomainDetailAsync(string idOrHostname, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(idOrHostname);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Domains,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchHostnameAsync(idOrHostname, requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        if (response.Hostname is null)
        {
          selectedHostnameId = null;
          selectedHostnameDetail = null;
          selectedHostnameVote = null;
          selectedUrlId = null;
          selectedUrlCanTriggerCrawl = false;
          ResetReportState();
          Groups = [];
          return;
        }

        selectedHostnameId = response.Hostname.Id;
        selectedHostnameDetail = response;
        selectedHostnameVote = response.ElectionVote?.Choice;
        selectedUrlId = null;
        selectedUrlCanTriggerCrawl = false;
        SetReportTarget(response.Hostname);
        remapGroups = () => MapHostnameDetailGroups(
            selectedHostnameDetail!,
            CanVoteSelectedHostname,
            CanUseSelectedHostnameActions,
            ViewerIsAuthenticated);
        Groups = remapGroups();
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadUrlDetailAsync(string id, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(id);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Urls,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchUrlAsync(id, requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        if (response.Url is null)
        {
          selectedHostnameId = null;
          selectedUrlId = null;
          selectedUrlCanTriggerCrawl = false;
          ResetReportState();
          Groups = [];
          return;
        }

        selectedHostnameId = null;
        selectedUrlId = response.Url.Id;
        selectedUrlCanTriggerCrawl = response.CanTriggerCrawl;
        SetReportTarget(response.Url.Hostname);
        remapGroups = () => MapUrlDetailGroups(response);
        Groups = remapGroups();
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadUrlCrawlsAsync(string webAddressId, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(webAddressId);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Urls,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchUrlCrawlsAsync(webAddressId, cancellationToken: requestCancellation)
          .ConfigureAwait(true);
      var reportHostname = await FetchReportHostnameAsync(webAddressId, requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        selectedHostnameId = null;
        selectedUrlId = webAddressId;
        selectedUrlCanTriggerCrawl = false;
        SetReportTarget(reportHostname);
        remapGroups = () => MapUrlCrawlListGroups(webAddressId, response);
        Groups = remapGroups();
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadUrlCrawlDetailAsync(
      string webAddressId,
      string crawlId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(webAddressId);
    ArgumentException.ThrowIfNullOrWhiteSpace(crawlId);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Urls,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchUrlCrawlAsync(webAddressId, crawlId, requestCancellation).ConfigureAwait(true);
      var reportHostname = await FetchReportHostnameAsync(webAddressId, requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        selectedHostnameId = null;
        selectedUrlId = webAddressId;
        selectedUrlCanTriggerCrawl = false;
        SetReportTarget(reportHostname);
        remapGroups = () => MapUrlCrawlDetailGroups(response);
        Groups = remapGroups();
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  private void ClearSelectedWebSearchState()
  {
    selectedHostnameId = null;
    selectedHostnameDetail = null;
    selectedHostnameVote = null;
    selectedUrlId = null;
    selectedUrlCanTriggerCrawl = false;
    ResetReportState();
    ActiveSurface = OmnisearchWebSearchSurface.Search;
    OnPropertyChanged(nameof(ActiveSurface));
    UpdateSelectedActionProperties();
  }

  private void UpdateSelectedActionProperties()
  {
    OnPropertyChanged(nameof(HasSelectedHostname));
    OnPropertyChanged(nameof(HasSelectedUrl));
    OnPropertyChanged(nameof(CanVoteSelectedHostname));
    OnPropertyChanged(nameof(CanClearSelectedHostnameVote));
    OnPropertyChanged(nameof(CanUseSelectedHostnameActions));
    OnPropertyChanged(nameof(CanTriggerSelectedUrlCrawl));
    OnPropertyChanged(nameof(HasSelectedReportTarget));
    OnPropertyChanged(nameof(CanReportSelectedHostname));
    OnPropertyChanged(nameof(ReportButtonText));
  }
}
