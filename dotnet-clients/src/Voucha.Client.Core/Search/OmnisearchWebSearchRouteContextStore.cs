using System.Diagnostics;

namespace Voucha.Client.Core.Search;

public enum OmnisearchWebSearchSurface
{
  Search,
  Domains,
  Urls,
}

public enum OmnisearchWebSearchDetailKind
{
  Detail,
  BookmarkedHostnames,
  BookmarkedUrls,
  UrlCrawls,
}

public sealed record OmnisearchWebSearchRouteContext(
    OmnisearchWebSearchSurface Surface,
    string? DetailId = null,
    string? SecondaryDetailId = null,
    OmnisearchWebSearchDetailKind DetailKind = OmnisearchWebSearchDetailKind.Detail);

public sealed class OmnisearchWebSearchRouteContextStore
{
  private object? context;

  public void Set(OmnisearchWebSearchRouteContext next)
  {
    ArgumentNullException.ThrowIfNull(next);
    var previous = Interlocked.Exchange(ref context, next);
    if (previous is not null)
    {
      Debug.WriteLine($"OmnisearchWebSearchRouteContextStore: overwriting un-consumed context {previous}");
    }
  }

  public void Clear() =>
      Interlocked.Exchange(ref context, null);

  public OmnisearchWebSearchRouteContext? Consume() =>
      (OmnisearchWebSearchRouteContext?)Interlocked.Exchange(ref context, null);
}

public static class OmnisearchResultRoute
{
  private const string DomainPrefix = "domain:";
  private const string UrlPrefix = "url:";
  private const string UrlCrawlsPrefix = "url-crawls:";
  private const string CrawlPrefix = "crawl:";

  public static string Domain(string idOrHostname) => DomainPrefix + idOrHostname;

  public static string WebAddress(string id) => UrlPrefix + id;

  public static string CrawlHistory(string id) => UrlCrawlsPrefix + id;

  public static string CrawlRecord(string webAddressId, string crawlId) => CrawlPrefix + webAddressId + ":" + crawlId;

  public static bool TryParseDomain(string? route, out string idOrHostname) =>
      TryParsePrefix(route, DomainPrefix, out idOrHostname);

  public static bool TryParseWebAddress(string? route, out string id) =>
      TryParsePrefix(route, UrlPrefix, out id);

  public static bool TryParseCrawlHistory(string? route, out string id) =>
      TryParsePrefix(route, UrlCrawlsPrefix, out id);

  public static bool TryParseCrawlRecord(string? route, out string webAddressId, out string crawlId)
  {
    webAddressId = string.Empty;
    crawlId = string.Empty;
    if (!TryParsePrefix(route, CrawlPrefix, out var remainder)) return false;

    var parts = remainder.Split(':', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return false;

    webAddressId = parts[0];
    crawlId = parts[1];
    return true;
  }

  private static bool TryParsePrefix(string? route, string prefix, out string value)
  {
    value = string.Empty;
    if (route is null || !route.StartsWith(prefix, StringComparison.Ordinal)) return false;

    value = route[prefix.Length..];
    return value.Length > 0;
  }
}
