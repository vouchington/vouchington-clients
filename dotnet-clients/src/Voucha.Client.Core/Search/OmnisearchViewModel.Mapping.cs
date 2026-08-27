using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public static IReadOnlyList<OmnisearchResultGroup> MapGroups(CombinedSearchResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    return MapGroups(response, UiLocalization.English);
  }

  private static OmnisearchResultGroup[] MapGroups(CombinedSearchResponse response, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(response);
    return new[]
      {
        TopicsGroup(response, localization), PostsGroup(response, localization), NewsGroup(response, localization), DomainsGroup(response, localization), CommunitiesGroup(response, localization),
      }
      .Where(group => group is not null)
      .Cast<OmnisearchResultGroup>()
      .ToArray();
  }

  public static IReadOnlyList<OmnisearchResultGroup> MapFediverseGroups(FediverseSearchResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    return MapFediverseGroups(response, UiLocalization.English);
  }

  private static OmnisearchResultGroup[] MapFediverseGroups(FediverseSearchResponse response, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(response);
    return response.Buckets
        .Select(bucket => new OmnisearchResultGroup(
            FediverseProviderTitle(bucket.Provider),
            FediverseRows(bucket, localization)))
        .Where(group => group.Rows.Count > 0)
        .ToArray();
  }

  private static OmnisearchResultRow[] FediverseRows(FediverseSearchBucket bucket, IUiLocalization localization)
  {
    var rows = bucket.Items
          .Select(item => new OmnisearchResultRow(
              FediverseResultKind(item.ResultType, localization),
              item.Title,
              FediverseResultDetail(item, localization),
              ExternalUrl: SafeHttpUrl(item.ExternalUrl)))
          .ToArray();
    return bucket.Status == "ok"
        ? rows
        : rows.Append(new OmnisearchResultRow(
            L(localization, UiMessageKey.NativeDotnetResidualStatus),
            bucket.Status == "partial"
                ? L(localization, UiMessageKey.NativeSwiftRouteSurfaceSomeResultsUnavailable)
                : F(localization, UiMessageKey.NativeDotnetResidualUnavailable, ("provider", FediverseProviderTitle(bucket.Provider))),
            bucket.ErrorCode ?? bucket.Status)).ToArray();
  }

  private static string FediverseResultKind(string resultType, IUiLocalization localization) =>
      resultType switch
      {
        "video" => L(localization, UiMessageKey.NativeDotnetResidualVideo),
        "profile" => L(localization, UiMessageKey.NativeDotnetResidualProfile),
        _ => L(localization, UiMessageKey.NativeDotnetResidualPost),
      };

  private static string FediverseProviderTitle(string provider) => provider switch
  {
    "peertube" => "PeerTube",
    _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(provider),
  };

  private static Uri? SafeHttpUrl(Uri url) =>
      url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps ? url : null;

  private static OmnisearchResultGroup? TopicsGroup(CombinedSearchResponse response, IUiLocalization localization) =>
      EmptyIfNull(response.Topics).Count == 0
          ? null
          : new OmnisearchResultGroup(
              L(localization, UiMessageKey.NativeDotnetResidualTopics),
              EmptyIfNull(response.Topics)
                  .Select(topic => new OmnisearchResultRow(
                      L(localization, UiMessageKey.NativeDotnetResidualTag),
                      F(localization, UiMessageKey.NativeDotnetResidualTopicTitle, ("name", topic.Name)),
                      topic.TopicType.Replace('_', ' ')))
                  .ToArray());

  private static OmnisearchResultGroup? PostsGroup(CombinedSearchResponse response, IUiLocalization localization) =>
      EmptyIfNull(response.Posts).Count == 0
          ? null
          : new OmnisearchResultGroup(
              L(localization, UiMessageKey.NativeDotnetResidualPosts),
              EmptyIfNull(response.Posts)
                  .Select(post => new OmnisearchResultRow(
                      L(localization, UiMessageKey.NativeDotnetResidualPost),
                      F(localization, UiMessageKey.NativeDotnetResidualPostTitle, ("title", post.Title)),
                      HumanizedPostType(post.PostType, localization)))
                  .ToArray());

  private static OmnisearchResultGroup? NewsGroup(CombinedSearchResponse response, IUiLocalization localization) =>
      EmptyIfNull(response.News).Count == 0
          ? null
          : new OmnisearchResultGroup(
              L(localization, UiMessageKey.NativeDotnetResidualNews),
              EmptyIfNull(response.News)
                  .Select(item => new OmnisearchResultRow(L(localization, UiMessageKey.NativeDotnetResidualNews), F(localization, UiMessageKey.NativeDotnetResidualArticleTitle, ("title", item.Title)), item.FeedTitle))
                  .ToArray());

  private static OmnisearchResultGroup? DomainsGroup(CombinedSearchResponse response, IUiLocalization localization) =>
      EmptyIfNull(response.Domains).Count == 0
          ? null
          : new OmnisearchResultGroup(
              L(localization, UiMessageKey.NativeDotnetResidualDomains),
              EmptyIfNull(response.Domains)
                  .Select(domain => new OmnisearchResultRow(
                      L(localization, UiMessageKey.NativeDotnetResidualDomain),
                      F(localization, UiMessageKey.NativeDotnetResidualDomainTitle, ("hostname", domain.Hostname)),
                      L(localization, UiMessageKey.NativeDotnetResidualDomain),
                      domain.Id is null ? null : OmnisearchResultRoute.Domain(domain.Id)))
                  .ToArray());

  private static OmnisearchResultGroup? CommunitiesGroup(CombinedSearchResponse response, IUiLocalization localization)
  {
    var communities = EmptyIfNull(response.Communities);
    if (communities.Count == 0) return null;

    return new OmnisearchResultGroup(
        L(localization, UiMessageKey.NativeDotnetResidualCommunities),
        communities
            .Where(community => community.Bookmarked)
            .Concat(communities.Where(community => !community.Bookmarked))
            .Select(community => new OmnisearchResultRow(
                L(localization, UiMessageKey.NativeDotnetResidualCommunity),
                F(localization, UiMessageKey.NativeDotnetResidualCommunityTitle, ("name", community.Name)),
                community.Bookmarked ? L(localization, UiMessageKey.NativeDotnetResidualBookmarked) : community.Slug))
            .ToArray());
  }

  private static string HumanizedPostType(string postType, IUiLocalization localization) =>
      postType switch
      {
        "discussion" => L(localization, UiMessageKey.NativeDotnetResidualDiscussion),
        "review" => L(localization, UiMessageKey.NativeDotnetResidualReview),
        "article" => L(localization, UiMessageKey.NativeDotnetResidualArticle),
        "blog_post" => L(localization, UiMessageKey.NativeDotnetResidualBlogPost),
        "story" => L(localization, UiMessageKey.NativeDotnetResidualStory),
        "link" => L(localization, UiMessageKey.NativeDotnetResidualLink),
        "data_point" => L(localization, UiMessageKey.NativeDotnetResidualDataPoint),
        "comment" => L(localization, UiMessageKey.NativeDotnetResidualComment),
        _ => postType.Replace('_', ' '),
      };

  private static IReadOnlyList<T> EmptyIfNull<T>(IReadOnlyList<T>? values) => values ?? [];

  private static string FediverseResultDetail(FediverseSearchResult item, IUiLocalization localization) =>
      string.IsNullOrWhiteSpace(item.AuthorName)
          ? item.SourceHostname ?? item.Provider
          : F(localization, UiMessageKey.NativeDotnetResidualAuthorOnSource, ("author", item.AuthorName), ("source", item.SourceHostname ?? item.Provider));
}

public enum OmnisearchMode
{
  Combined,
  Fediverse,
}

public sealed record OmnisearchResultGroup(string Title, IReadOnlyList<OmnisearchResultRow> Rows);

public sealed record OmnisearchResultRow(
    string Kind,
    string Title,
    string Detail,
    string? Route = null,
    string? PrimaryAction = null,
    Uri? ExternalUrl = null)
{
  public bool CanOpen => Route is not null || PrimaryAction is not null || ExternalUrl is not null;
}
