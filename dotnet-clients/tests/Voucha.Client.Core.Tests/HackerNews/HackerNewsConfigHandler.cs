using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.HackerNews;

internal sealed class HackerNewsConfigHandler(string? configJson, HttpStatusCode configStatus = HttpStatusCode.OK)
  : HttpMessageHandler
{
  public const string ConfigPath = "/api/v1/hacker-news-config";

  public static string FixtureOn() => ApiFixtureLoader.LoadResponse("native.hacker-news-config.default");

  public static string Off() => JsonSerializer.Serialize(new { references_visible = false, post_time_range_cap = "1y" });

  public List<string> Paths { get; } = [];

  public IEnumerable<string> ContentPaths => Paths.Where(path => path != ConfigPath);

  public VouchaApiClient Client() =>
      new(new HttpClient(this) { BaseAddress = new Uri("https://api.test") });

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var pathAndQuery = request.RequestUri!.PathAndQuery;
    Paths.Add(pathAndQuery);
    if (pathAndQuery == ConfigPath)
      return Task.FromResult(new HttpResponseMessage(configStatus) { Content = new StringContent(configJson ?? "{}") });
    var body = pathAndQuery.StartsWith("/api/v1/posts", StringComparison.Ordinal)
        ? """{"results":[],"posts":{},"users":{},"communities":{},"page_info":{"has_next_page":false}}"""
        : """{"results":[],"rss_feed_items":{},"rss_feeds":{},"page_info":{"has_next_page":false}}""";
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
  }
}
