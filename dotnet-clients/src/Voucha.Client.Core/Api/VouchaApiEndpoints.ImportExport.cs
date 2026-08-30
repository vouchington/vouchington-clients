namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest ImportTopics(IReadOnlyList<string> names) =>
      new(HttpMethod.Post, "/api/v1/my/import/topics") { Body = new TopicImportBody(names) };

  public static ApiRequest ExportTopics() => Get("/api/v1/my/export/topics");

  public static ApiRequest ExportTopicsDownload() => Get("/api/v1/my/export/topics") with
  {
    Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["download"] = "1" },
  };

  public static ApiRequest ImportRssFeedUrls(IReadOnlyList<string> urls) =>
      new(HttpMethod.Post, "/api/v1/my/import/rss-feeds") { Body = new RssFeedUrlsImportBody(urls) };

  public static ApiRequest ImportRssFeedCsv(string csv) =>
      new(HttpMethod.Post, "/api/v1/my/import/rss-feeds") { Body = new RssFeedCsvImportBody(csv) };

  public static ApiRequest ImportRssFeedOpml(string opml) =>
      new(HttpMethod.Post, "/api/v1/my/import/rss-feeds") { Body = new RssFeedOpmlImportBody(opml) };

  public static ApiRequest RssFeedImportStatus(string importId) =>
      Get($"/api/v1/my/import/rss-feeds/{Path(importId)}");

  public static ApiRequest ExportRssFeeds(string? feedType, string format)
  {
    var query = new Dictionary<string, string>(StringComparer.Ordinal);
    if (feedType is not null) query["feed_type"] = feedType;
    if (format == "csv") query["format"] = "csv";
    return Get("/api/v1/my/export/rss-feeds") with
    {
      Query = query,
      Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["Accept"] = format == "csv" ? "text/csv" : "text/xml",
      },
    };
  }
}
