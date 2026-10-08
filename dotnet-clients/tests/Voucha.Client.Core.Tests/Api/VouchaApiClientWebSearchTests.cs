using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task SearchHostnamesAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.hostnames.default");

    var response = await client.SearchHostnamesAsync(
        query: "example",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/hostnames?limit=25&query=example");
    Assert.Equal("hostname-1", response.Results[0].Id);
    Assert.Equal("example.com", response.Hostnames["hostname-1"].HostnameValue);
    Assert.Equal(10, response.HostnameElections?["hostname-1"].VotesScoreNet);
    Assert.Equal("url-1", response.TopUrlsByHostnameId?["hostname-1"][0].Id);
  }

  [Fact]
  public async Task FetchHostnameAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.hostname.default");

    var response = await client.FetchHostnameAsync(
        "hostname-1",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/hostnames/hostname-1");
    Assert.Equal("example.com", response.Hostname.HostnameValue);
    Assert.Null(response.Topic);
    Assert.Null(response.ElectionVote);
    Assert.Equal("Example Feed", response.RssFeeds[0].Title);
    Assert.Equal("Example Topic", response.RssFeeds[0].Topic?.Name);
  }

  [Fact]
  public async Task SearchUrlsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.urls.default");

    var response = await client.SearchUrlsAsync(
        query: "example",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/urls?limit=25&query=example");
    Assert.Equal("url-1", response.Results[0].Id);
    Assert.Equal("https://example.com/guides/native-clients", response.Results[0].UrlValue);
    Assert.Equal("example.com", response.Results[0].Hostname?.HostnameValue);
  }

  [Fact]
  public async Task FetchUserUrlsAsyncUsesCollectionRoute()
  {
    var (client, handler) = CreateClient("native.urls.default");

    var response = await client.FetchUserUrlsAsync(
        "alice",
        "saved",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/alice/urls/saved?limit=25");
    Assert.Equal("url-1", response.Results[0].Id);
  }

  [Fact]
  public async Task FetchUserHostnamesAsyncUsesCollectionRoute()
  {
    var handler = new RecordingHandler("""
        {
          "results": [
            {
              "__entity_type": "hostname",
              "id": "hostname-1",
              "hostname": "example.com",
              "is_blocked": false,
              "is_crawlable": true
            }
          ],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchUserHostnamesAsync(
        "alice",
        "muted",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/alice/domains/muted?limit=25");
    Assert.Equal("hostname-1", response.Results[0].Id);
  }

  [Fact]
  public async Task FetchUrlAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.url.default");

    var response = await client.FetchUrlAsync("url-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/urls/url-1");
    Assert.True(response.CanViewCrawlHistory);
    Assert.True(response.CanTriggerCrawl);
    Assert.Null(response.LatestCrawl);
    Assert.Equal("url", response.UrlType);
  }

  [Fact]
  public async Task FetchUrlCrawlsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.url-crawls.default");

    var response = await client.FetchUrlCrawlsAsync("url-1", cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/urls/url-1/crawls?limit=25");
    Assert.Equal("crawl-1", response.Results[0].Id);
    Assert.Equal(200, response.Results[0].ResponseStatusCode);
  }

  [Fact]
  public async Task FetchUrlCrawlAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.url-crawl.default");

    var response = await client.FetchUrlCrawlAsync("url-1", "crawl-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/urls/url-1/crawls/crawl-1");
    Assert.Equal("crawl-1", response.Crawl.Id);
    Assert.Null(response.OgImageSideload);
  }

  [Fact]
  public async Task FetchRssFeedCrawlsAsyncUsesPaidSafeFixtureAndCursor()
  {
    var (client, handler) = CreateClient("web.paid.rss-feed-crawls.default");

    var response = await client.FetchRssFeedCrawlsAsync(
        "rss-feed-1",
        after: "cursor-1",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/rss-feeds/rss-feed-1/crawls?after=cursor-1&limit=25");
    Assert.Equal("crawl-1", response.Results[0].Id);
    Assert.Equal(200, response.Results[0].ResponseCode);
  }

  [Fact]
  public async Task FetchUrlCrawlAsyncAcceptsBufferJsonHash()
  {
    var handler = new RecordingHandler("""
        {
          "crawl": {
            "id": "crawl-1",
            "url_id": "url-1",
            "created_at": "2026-01-01T00:00:00Z",
            "completed_at": null,
            "response_status_code": 200,
            "has_pending_embeddings": false,
            "html_sha256": { "type": "Buffer", "data": [1, 2, 3] }
          },
          "og_image_sideload": null
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchUrlCrawlAsync("url-1", "crawl-1", TestContext.Current.CancellationToken);

    Assert.True(response.Crawl.HtmlSha256.HasValue);
    var hash = response.Crawl.HtmlSha256.GetValueOrDefault();
    Assert.Equal(JsonValueKind.Object, hash.ValueKind);
    Assert.Equal("Buffer", hash.GetProperty("type").GetString());
    Assert.Equal(3, hash.GetProperty("data").GetArrayLength());
  }

  [Fact]
  public async Task FetchUrlCrawlAsyncPreservesEmbedResolutionFields()
  {
    var handler = new RecordingHandler("""
        {
          "crawl": {
            "id": "crawl-1",
            "url_id": "url-1",
            "created_at": "2026-09-01T00:00:00Z",
            "completed_at": null,
            "response_status_code": 200,
            "embed_metadata": {
              "title": "Example",
              "provider": { "key": "youtube", "name": "YouTube" }
            },
            "embed_oembed_url": "https://www.youtube.com/oembed",
            "embed_oembed_resolved_at": "2026-09-01T00:00:00Z"
          },
          "og_image_sideload": null
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchUrlCrawlAsync("url-1", "crawl-1", TestContext.Current.CancellationToken);

    Assert.True(response.Crawl.EmbedMetadata.HasValue);
    Assert.Equal("Example", response.Crawl.EmbedMetadata.Value.GetProperty("title").GetString());
    Assert.Equal("youtube", response.Crawl.EmbedMetadata.Value.GetProperty("provider").GetProperty("key").GetString());
    Assert.Equal("https://www.youtube.com/oembed", response.Crawl.EmbedOembedUrl);
    Assert.Equal(DateTimeOffset.Parse("2026-09-01T00:00:00Z"), response.Crawl.EmbedOembedResolvedAt);

    var encoded = JsonSerializer.Serialize(response.Crawl, VouchaApiJson.Options);
    using var document = JsonDocument.Parse(encoded);
    var crawl = document.RootElement;
    Assert.Equal("Example", crawl.GetProperty("embed_metadata").GetProperty("title").GetString());
    Assert.Equal(
        "youtube",
        crawl.GetProperty("embed_metadata").GetProperty("provider").GetProperty("key").GetString());
    Assert.Equal("https://www.youtube.com/oembed", crawl.GetProperty("embed_oembed_url").GetString());
    Assert.Equal(
        "2026-09-01T00:00:00+00:00",
        crawl.GetProperty("embed_oembed_resolved_at").GetString());
  }

  [Fact]
  public async Task TriggerUrlCrawlAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.url-crawl-trigger.default");

    var response = await client.TriggerUrlCrawlAsync("url-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/urls/url-1/crawl");
    Assert.True(response.Success);
    Assert.Equal("Crawl enqueued", response.Message);
  }

  [Fact]
  public void ReportBodyUsesBackendJsonFieldNames()
  {
    var json = JsonSerializer.Serialize(
        new ReportBody("url_hostname", "hostname-1", "spam", "note", "turnstile"),
        VouchaApiJson.Options);

    Assert.Contains("\"entityType\":\"url_hostname\"", json, StringComparison.Ordinal);
    Assert.Contains("\"entityId\":\"hostname-1\"", json, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile\"", json, StringComparison.Ordinal);
  }
}
