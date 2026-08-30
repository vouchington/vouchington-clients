using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ImportExportApiTests
{
  [Fact]
  public void EndpointsEncodeEveryImportBodyAndExportFormat()
  {
    AssertBody(VouchaApiEndpoints.ImportTopics(["Travel"]), "{\"names\":[\"Travel\"]}");
    AssertBody(
        VouchaApiEndpoints.ImportRssFeedUrls(["https://example.test/feed.xml"]),
        "{\"urls\":[\"https://example.test/feed.xml\"],\"follow\":true}");
    AssertBody(VouchaApiEndpoints.ImportRssFeedCsv("url\na"), "{\"csv\":\"url\\na\",\"follow\":true}");
    AssertBody(VouchaApiEndpoints.ImportRssFeedOpml("<opml/>"), "{\"opml\":\"\\u003Copml/\\u003E\",\"follow\":true}");
    Assert.Equal("1", VouchaApiEndpoints.ExportTopicsDownload().Query["download"]);

    var csv = VouchaApiEndpoints.ExportRssFeeds("podcast", "csv");
    var opml = VouchaApiEndpoints.ExportRssFeeds(null, "opml");
    Assert.Equal("podcast", csv.Query["feed_type"]);
    Assert.Equal("csv", csv.Query["format"]);
    Assert.Equal("text/csv", csv.Headers["Accept"]);
    Assert.Empty(opml.Query);
    Assert.Equal("text/xml", opml.Headers["Accept"]);
  }

  [Fact]
  public async Task RawTextTransportAppliesHeadersAndPreservesApiErrors()
  {
    var success = new HeaderHandler("a,b", HttpStatusCode.OK);
    var client = new VouchaApiClient(new HttpClient(success) { BaseAddress = new("https://api.test") });

    var text = await client.ExportRssFeedsAsync("article", "csv", TestContext.Current.CancellationToken);

    Assert.Equal("a,b", text);
    Assert.Equal("text/csv", success.Accept);
    Assert.Equal("/api/v1/my/export/rss-feeds?feed_type=article&format=csv", success.Path);

    var failure = new HeaderHandler("too large", HttpStatusCode.RequestEntityTooLarge);
    client = new(new HttpClient(failure) { BaseAddress = new("https://api.test") });
    var error = await Assert.ThrowsAsync<VouchaApiException>(
        () => client.ExportRssFeedsAsync(null, "opml", TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.RequestEntityTooLarge, error.StatusCode);
    Assert.Equal("too large", error.ResponseBody);
  }

  [Fact]
  public async Task TopicExportArtifactIsTheJsonResultsArray()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.import-export.topics.export.default"));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") });
    var service = new ApiImportExportService(client);

    var document = await service.ExportTopicsAsync(TestContext.Current.CancellationToken);
    using var json = JsonDocument.Parse(document.Contents);

    Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    Assert.Equal("Local News", json.RootElement[0].GetProperty("name").GetString());
  }

  [Theory]
  [InlineData(null, SourceExportFormat.Csv, "rss-feeds.csv", "text/csv", "/api/v1/my/export/rss-feeds?format=csv")]
  [InlineData("article", SourceExportFormat.Csv, "rss-feeds.csv", "text/csv", "/api/v1/my/export/rss-feeds?feed_type=article&format=csv")]
  [InlineData("podcast", SourceExportFormat.Csv, "rss-feeds.csv", "text/csv", "/api/v1/my/export/rss-feeds?feed_type=podcast&format=csv")]
  [InlineData("video", SourceExportFormat.Csv, "rss-feeds.csv", "text/csv", "/api/v1/my/export/rss-feeds?feed_type=video&format=csv")]
  [InlineData(null, SourceExportFormat.Opml, "rss-feeds.opml", "text/xml", "/api/v1/my/export/rss-feeds")]
  [InlineData("article", SourceExportFormat.Opml, "rss-feeds.opml", "text/xml", "/api/v1/my/export/rss-feeds?feed_type=article")]
  [InlineData("podcast", SourceExportFormat.Opml, "rss-feeds.opml", "text/xml", "/api/v1/my/export/rss-feeds?feed_type=podcast")]
  [InlineData("video", SourceExportFormat.Opml, "rss-feeds.opml", "text/xml", "/api/v1/my/export/rss-feeds?feed_type=video")]
  public async Task SourceExportMatrixCreatesAndSharesExactNativeDocument(
      string? feedType,
      SourceExportFormat format,
      string fileName,
      string mediaType,
      string path)
  {
    var handler = new HeaderHandler("export bytes", HttpStatusCode.OK);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") });
    var service = new ApiImportExportService(client);
    var sharer = new CapturingDocumentSharer();
    var adapter = new ImportExportFileAdapter(new NullPicker(), new StrictUtf8ImportExportFileReader(), sharer);

    var document = await service.ExportSourcesAsync(feedType, format, TestContext.Current.CancellationToken);
    await adapter.ShareAsync(document, TestContext.Current.CancellationToken);

    Assert.Equal(fileName, document.FileName);
    Assert.Equal(mediaType, document.MediaType);
    Assert.Equal("export bytes", Encoding.UTF8.GetString(document.Contents.Span));
    Assert.Equal(path, handler.Path);
    Assert.Equal(mediaType, handler.Accept);
    Assert.Same(document, sharer.Document);
  }

  [Theory]
  [InlineData("native.import-export.rss-feeds.submit.default", typeof(RssFeedImportSubmission))]
  [InlineData("native.import-export.rss-feeds.status.retrying", typeof(RssFeedImportStatus))]
  [InlineData("native.import-export.rss-feeds.status.partial", typeof(RssFeedImportStatus))]
  [InlineData("native.import-export.topics.import.outcomes", typeof(TopicImportResponse))]
  [InlineData("native.import-export.topics.export.default", typeof(TopicExportResponse))]
  [InlineData("native.import-export.topics.export.download", typeof(ExportTopic[]))]
  public void SharedImportExportFixturesDecodeIntoTypedDtos(string fixtureId, Type type)
  {
    var decoded = JsonSerializer.Deserialize(ApiFixtureLoader.LoadResponse(fixtureId), type, VouchaApiJson.Options);
    Assert.NotNull(decoded);
  }

  private static void AssertBody(ApiRequest request, string expected) =>
      Assert.Equal(expected, JsonSerializer.Serialize(request.Body, request.Body!.GetType(), VouchaApiJson.Options));

  private sealed class HeaderHandler(string body, HttpStatusCode status) : HttpMessageHandler
  {
    public string? Accept { get; private set; }
    public string? Path { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
      Accept = request.Headers.Accept.Single().MediaType;
      Path = request.RequestUri?.PathAndQuery;
      return Task.FromResult(new HttpResponseMessage(status)
      {
        Content = new StringContent(body, Encoding.UTF8, "text/plain"),
      });
    }
  }

  private sealed class NullPicker : IImportExportFilePicker
  {
    public Task<PickedImportFile?> PickAsync(CancellationToken token) => Task.FromResult<PickedImportFile?>(null);
  }

  private sealed class CapturingDocumentSharer : IImportExportDocumentSharer
  {
    public ExportDocument? Document { get; private set; }
    public Task ShareAsync(ExportDocument document, CancellationToken token)
    {
      Document = document;
      return Task.CompletedTask;
    }
  }
}
