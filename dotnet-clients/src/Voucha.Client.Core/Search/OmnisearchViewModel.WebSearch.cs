using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private readonly OmnisearchWebSearchRouteContextStore? routeContextStore;
  private string? selectedHostnameId;
  private HostnameDetailResponse? selectedHostnameDetail;
  private string? selectedUrlId;
  private bool selectedUrlCanTriggerCrawl;

  public OmnisearchWebSearchSurface ActiveSurface { get; private set; } = OmnisearchWebSearchSurface.Search;

  public bool HasSelectedHostname => selectedHostnameId is not null;

  public bool HasSelectedUrl => selectedUrlId is not null;

  public bool CanVoteSelectedHostname =>
      selectedHostnameId is { } id && ViewerCanCastPublicVotes && !votingHostnameIds.Contains(id);

  public bool CanUseSelectedHostnameActions => selectedHostnameId is not null && ViewerIsAuthenticated;

  public bool CanTriggerSelectedUrlCrawl =>
      selectedUrlId is not null &&
      selectedUrlCanTriggerCrawl &&
      ViewerCanTriggerUrlCrawls;

  public async Task LoadRouteContextAsync(CancellationToken cancellationToken = default)
  {
    var context = routeContextStore?.Consume();
    if (context is null) return;

    switch (context)
    {
      case { Surface: OmnisearchWebSearchSurface.Domains, DetailId: { Length: > 0 } id }:
        if (context.DetailKind == OmnisearchWebSearchDetailKind.BookmarkedHostnames)
        {
          await LoadBookmarkedDomainsAsync(id, cancellationToken).ConfigureAwait(true);
        }
        else
        {
          await LoadDomainDetailAsync(id, cancellationToken).ConfigureAwait(true);
        }
        break;
      case { Surface: OmnisearchWebSearchSurface.Domains }:
        await LoadDomainsAsync(cancellationToken).ConfigureAwait(true);
        break;
      case { Surface: OmnisearchWebSearchSurface.Urls, DetailId: { Length: > 0 } id }:
        if (context.DetailKind == OmnisearchWebSearchDetailKind.BookmarkedUrls)
        {
          await LoadBookmarkedUrlsAsync(id, cancellationToken).ConfigureAwait(true);
        }
        else if (context.SecondaryDetailId is { Length: > 0 } crawlId)
        {
          await LoadUrlCrawlDetailAsync(id, crawlId, cancellationToken).ConfigureAwait(true);
        }
        else if (context.DetailKind == OmnisearchWebSearchDetailKind.UrlCrawls)
        {
          await LoadUrlCrawlsAsync(id, cancellationToken).ConfigureAwait(true);
        }
        else
        {
          await LoadUrlDetailAsync(id, cancellationToken).ConfigureAwait(true);
        }
        break;
      case { Surface: OmnisearchWebSearchSurface.Urls }:
        await LoadUrlsAsync(cancellationToken).ConfigureAwait(true);
        break;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native search surfaces API failures in view state before MAUI async handlers observe them.")]
  public async Task LoadDomainsAsync(CancellationToken cancellationToken = default)
  {
    var queryText = TrimmedQueryOrNull();
    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Domains,
        async (requestCancellation, apply) =>
    {
      var response = await client.SearchHostnamesAsync(query: queryText, limit: 25, cancellationToken: requestCancellation)
          .ConfigureAwait(true);
      apply(() =>
      {
        remapGroups = () => MapHostnameListGroups(response);
        Groups = remapGroups();
        selectedHostnameId = null;
        selectedUrlId = null;
        selectedUrlCanTriggerCrawl = false;
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native search surfaces API failures in view state before MAUI async handlers observe them.")]
  public async Task LoadUrlsAsync(CancellationToken cancellationToken = default)
  {
    var queryText = TrimmedQueryOrNull();
    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Urls,
        async (requestCancellation, apply) =>
    {
      var response = await client.SearchUrlsAsync(query: queryText, limit: 25, cancellationToken: requestCancellation)
          .ConfigureAwait(true);
      apply(() =>
      {
        remapGroups = () => MapUrlListGroups(response);
        Groups = remapGroups();
        selectedHostnameId = null;
        selectedUrlId = null;
        selectedUrlCanTriggerCrawl = false;
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadBookmarkedUrlsAsync(string listType, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(listType);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Urls,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchUserUrlsAsync(
          CurrentUserIdOrUsername(),
          listType,
          limit: 25,
          cancellationToken: requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        remapGroups = () => MapUrlListGroups(response);
        Groups = remapGroups();
        selectedHostnameId = null;
        selectedUrlId = null;
        selectedUrlCanTriggerCrawl = false;
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadBookmarkedDomainsAsync(string listType, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(listType);

    await RunWebSearchLoadAsync(
        OmnisearchWebSearchSurface.Domains,
        async (requestCancellation, apply) =>
    {
      var response = await client.FetchUserHostnamesAsync(
          CurrentUserIdOrUsername(),
          listType,
          limit: 25,
          cancellationToken: requestCancellation).ConfigureAwait(true);
      apply(() =>
      {
        remapGroups = () => MapHostnameCollectionGroups(response);
        Groups = remapGroups();
        selectedHostnameId = null;
        selectedUrlId = null;
        selectedUrlCanTriggerCrawl = false;
      });
    },
        cancellationToken).ConfigureAwait(true);
  }

  public Task OpenRowAsync(OmnisearchResultRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);

    if (OmnisearchResultRoute.TryParseDomain(row.Route, out var domainId))
    {
      return LoadDomainDetailAsync(domainId, cancellationToken);
    }

    if (OmnisearchResultRoute.TryParseWebAddress(row.Route, out var urlId))
    {
      return LoadUrlDetailAsync(urlId, cancellationToken);
    }

    if (OmnisearchResultRoute.TryParseCrawlHistory(row.Route, out var crawlsUrlId))
    {
      return LoadUrlCrawlsAsync(crawlsUrlId, cancellationToken);
    }

    if (OmnisearchResultRoute.TryParseCrawlRecord(row.Route, out var crawlUrlId, out var crawlId))
    {
      return LoadUrlCrawlDetailAsync(crawlUrlId, crawlId, cancellationToken);
    }

    return RunSelectedPrimaryActionAsync(row.PrimaryAction, cancellationToken);
  }

}
