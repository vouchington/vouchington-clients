using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private IReadOnlyList<OmnisearchResultGroup> MapHostnameListGroups(HostnamesResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    var results = response.Results ?? [];
    var hostnames = response.Hostnames ?? new Dictionary<string, Hostname>();
    var ordered = results.Count == 0
        ? hostnames.Values
        : results
            .Select(reference => reference.EntityId ?? reference.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => hostnames.GetValueOrDefault(id!))
            .Where(hostname => hostname is not null)
            .Cast<Hostname>();

    var elections = response.HostnameElections ?? new Dictionary<string, HostnameElection>();
    var rows = ordered
        .Select(hostname => HostnameListRow(hostname, elections.GetValueOrDefault(hostname.Id)))
        .ToArray();

    return rows.Length == 0 ? [] : [new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualDomains), rows)];
  }

  private IReadOnlyList<OmnisearchResultGroup> MapHostnameCollectionGroups(HostnameCollectionResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    var rows = (response.Results ?? []).Select(hostname => HostnameListRow(hostname, null)).ToArray();
    return rows.Length == 0 ? [] : [new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualDomains), rows)];
  }

  private OmnisearchResultGroup[] MapHostnameDetailGroups(
      HostnameDetailResponse response,
      bool includeVoteActions,
      bool includeModerationActions,
      bool includeTopUrlRoutes)
  {
    var detailRows = new List<OmnisearchResultRow>
    {
      new(L(localization, UiMessageKey.NativeDotnetResidualDomain), response.Hostname.HostnameValue, HostnameStateDetail(response.Hostname)),
    };
    if (response.Topic is not null)
    {
      detailRows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualTopic), response.Topic.Name, response.Topic.TopicType?.Replace('_', ' ') ?? L(localization, UiMessageKey.NativeDotnetResidualTopic)));
    }

    if (response.HostnameElection is not null)
    {
      detailRows.Add(new(
          L(localization, UiMessageKey.NativeDotnetOmnisearchTrust),
          TrustBadge(response.HostnameElection),
          F(localization, UiMessageKey.NativeDotnetResidualVotes, ("up", response.HostnameElection.VotesCountUp), ("down", response.HostnameElection.VotesCountDown))));
    }

    var actionRows = new List<OmnisearchResultRow>();
    if (includeVoteActions)
    {
      actionRows.Add(new(L(localization, UiMessageKey.ExtractedVotesSemanticVoteVote), L(localization, UiMessageKey.ExtractedVotesSemanticVoteLike), L(localization, UiMessageKey.ExtractedVotesSemanticVoteVote), PrimaryAction: "sentiment.like"));
      actionRows.Add(new(L(localization, UiMessageKey.ExtractedVotesSemanticVoteVote), L(localization, UiMessageKey.ExtractedVotesSemanticVoteDislike), L(localization, UiMessageKey.ExtractedVotesSemanticVoteVote), PrimaryAction: "sentiment.dislike"));
    }

    if (includeModerationActions)
    {
      actionRows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualModeration), L(localization, UiMessageKey.NativeDotnetResidualMuteDomain), L(localization, UiMessageKey.NativeDotnetResidualHideDomain), PrimaryAction: "Mute"));
      actionRows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualModeration), L(localization, UiMessageKey.NativeDotnetResidualBlockDomain), L(localization, UiMessageKey.NativeDotnetResidualBlockDomainDetail), PrimaryAction: "Block"));
    }

    var topUrlRows = (response.TopUrls ?? [])
        .Select(url => new OmnisearchResultRow(
            L(localization, UiMessageKey.NativeDotnetResidualUrl),
            url.Url,
            url.Pathname,
            includeTopUrlRoutes ? OmnisearchResultRoute.WebAddress(url.Id) : null))
        .ToArray();

    return new[]
      {
        new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualDomainDetail), detailRows),
        actionRows.Count == 0 ? null : new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualDomainActions), actionRows),
        topUrlRows.Length == 0 ? null : new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualTopUrls), topUrlRows),
      }
      .Where(group => group is not null)
      .Cast<OmnisearchResultGroup>()
      .ToArray();
  }

  private IReadOnlyList<OmnisearchResultGroup> MapUrlListGroups(UrlSearchResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    var rows = (response.Results ?? [])
        .Select(url => new OmnisearchResultRow(
            L(localization, UiMessageKey.NativeDotnetResidualUrl),
            url.UrlValue,
            url.Hostname?.HostnameValue ?? url.Pathname ?? L(localization, UiMessageKey.NativeDotnetResidualUrl),
            OmnisearchResultRoute.WebAddress(url.Id)))
        .ToArray();

    return rows.Length == 0 ? [] : [new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualUrls), rows)];
  }

  private OmnisearchResultGroup[] MapUrlDetailGroups(UrlDetailResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);
    if (response.Url is null) return [];

    var detailRows = new List<OmnisearchResultRow>
    {
      new(L(localization, UiMessageKey.NativeDotnetResidualUrl), response.Url.UrlValue, response.Url.Hostname?.HostnameValue ?? response.UrlType),
    };
    if (response.Url.CanonicalUrlId is not null)
    {
      detailRows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualCanonicalUrl), response.Url.CanonicalUrlId, L(localization, UiMessageKey.NativeDotnetResidualCanonicalTarget)));
    }

    var crawlRows = new List<OmnisearchResultRow>();
    if (response.CanViewLatestCrawl && response.LatestCrawl is not null)
    {
      crawlRows.Add(CrawlRow(response.Url.Id, response.LatestCrawl, L(localization, UiMessageKey.NativeDotnetResidualLatestCrawl)));
    }

    if (response.CanViewCrawlHistory)
    {
      crawlRows.Add(new(
          L(localization, UiMessageKey.NativeDotnetResidualCrawlHistory),
          L(localization, UiMessageKey.NativeDotnetResidualHistoryAvailable),
          L(localization, UiMessageKey.NativeDotnetResidualOpenCrawlHistory),
          OmnisearchResultRoute.CrawlHistory(response.Url.Id)));
    }

    if (response.CanTriggerCrawl)
    {
      crawlRows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualCrawl), L(localization, UiMessageKey.NativeDotnetOmnisearchTriggerCrawl), L(localization, UiMessageKey.NativeDotnetResidualNativeCrawlAction), PrimaryAction: "Trigger crawl"));
    }

    return new[]
      {
        new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualUrlDetail), detailRows),
        crawlRows.Count == 0 ? null : new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualCrawlControls), crawlRows),
      }
      .Where(group => group is not null)
      .Cast<OmnisearchResultGroup>()
      .ToArray();
  }

  private OmnisearchResultRow HostnameListRow(Hostname hostname, HostnameElection? election)
  {
    var details = new[]
      {
        election is not null ? TrustBadge(election) : null,
        hostname.Blocked == true ? L(localization, UiMessageKey.NativeDotnetResidualBlocked) : null,
        hostname.Crawlable is bool crawlable ? L(localization, crawlable ? UiMessageKey.NativeDotnetResidualCrawlable : UiMessageKey.NativeDotnetResidualNotCrawlable) : null,
        hostname.LinkRelFollow == true ? L(localization, UiMessageKey.NativeDotnetResidualFollowsLinks) : null,
      }
      .Where(value => value is not null)
      .ToArray();

    return new(
        L(localization, UiMessageKey.NativeDotnetResidualDomain),
        hostname.HostnameValue,
        string.Join(", ", details),
        OmnisearchResultRoute.Domain(hostname.Id));
  }

  private string HostnameStateDetail(Hostname hostname) =>
      hostname.Blocked == true
          ? L(localization, UiMessageKey.NativeDotnetResidualBlockedDomain)
          : hostname.Crawlable is bool crawlable
              ? L(localization, crawlable ? UiMessageKey.NativeDotnetResidualCrawlableDomain : UiMessageKey.NativeDotnetResidualNotCrawlableDomain)
              : L(localization, UiMessageKey.NativeDotnetResidualDomain);

  private string TrustBadge(HostnameElection election) =>
      election switch
      {
        { VotesScoreNet: >= 3, VotesCountUp: >= 5 } => L(localization, UiMessageKey.NativeDotnetResidualTrusted),
        { VotesScoreNet: <= -3 } => L(localization, UiMessageKey.NativeDotnetResidualDistrusted),
        { VotesCountUp: 0, VotesCountDown: 0 } => L(localization, UiMessageKey.NativeDotnetResidualUnrated),
        _ => L(localization, UiMessageKey.NativeDotnetResidualNeutral),
      };

}
