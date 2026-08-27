using System.Net;
using System.Text;
using Voucha.Client.Core.HnDiscussions;
using Xunit;

namespace Voucha.Client.Core.Tests.HnDiscussions;

public sealed class HnDiscussionUrlCollectorTests
{
  [Fact]
  public void NormalizeLowercasesHostAndStripsHashSlashAndUtm()
  {
    Assert.Equal(
        "https://news.ycombinator.com/item?id=1",
        HnDiscussionUrlCollector.Normalize(
            "https://News.YCombinator.com/item/?utm_source=x&id=1#comments"));
  }

  [Fact]
  public void NormalizeRejectsNonHttpUrls()
  {
    Assert.Null(HnDiscussionUrlCollector.Normalize("not a url"));
    Assert.Null(HnDiscussionUrlCollector.Normalize("javascript:alert(1)"));
  }

  [Fact]
  public void CollectDedupesAndCapsAtThree()
  {
    Assert.Equal(
        [
          "https://example.com/a/",
          "https://example.com/b?utm_campaign=1",
          "https://example.com/c",
        ],
        HnDiscussionUrlCollector.Collect([
          "https://example.com/a/",
          "https://EXAMPLE.com/a",
          "https://example.com/b?utm_campaign=1",
          "https://example.com/c",
          "https://example.com/d",
        ]));
  }

  [Fact]
  public void ExtractPullsHttpUrlsFromMarkdown()
  {
    Assert.Equal(
        ["https://example.com/a", "https://example.com/b"],
        HnDiscussionUrlCollector.ExtractFromMarkdown("See https://example.com/a and https://example.com/b."));
  }
}

public sealed class HnDiscussionsClientTests
{
  [Fact]
  public void MapHitsKeepsMatchingTitleScoreAndComments()
  {
    const string json = """
        {"hits":[
          {"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18},
          {"objectID":"999","title":"Other","url":"https://other.example/a","points":1,"num_comments":0},
          {"objectID":"124","title":"","url":"https://example.com/a","points":2,"num_comments":2}
        ]}
        """;
    var threads = HnDiscussionsClient.MapHits(json, "https://example.com/a");
    var thread = Assert.Single(threads);
    Assert.Equal("123", thread.ObjectId);
    Assert.Equal("Example", thread.Title);
    Assert.Equal(42, thread.Score);
    Assert.Equal(18, thread.CommentCount);
    Assert.Equal(new Uri("https://news.ycombinator.com/item?id=123"), thread.ItemUrl);
  }

  [Fact]
  public void BuildSearchUrlRestrictsToStoryUrls()
  {
    var uri = HnDiscussionsClient.BuildSearchUrl("https://example.com/a");
    Assert.Equal("hn.algolia.com", uri.Host);
    Assert.Equal("/api/v1/search", uri.AbsolutePath);
    Assert.Contains("query=https%3A%2F%2Fexample.com%2Fa", uri.Query, StringComparison.Ordinal);
    Assert.Contains("restrictSearchableAttributes=url", uri.Query, StringComparison.Ordinal);
    Assert.Contains("tags=story", uri.Query, StringComparison.Ordinal);
    Assert.Contains("hitsPerPage=5", uri.Query, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SearchAsyncReturnsMatchingThreadsAndSkipsFailedResponses()
  {
    var handler = new QueueHandler(
        ("""{"hits":[{"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18}]}""",
            HttpStatusCode.OK),
        ("{}", HttpStatusCode.ServiceUnavailable));
    var client = new HnDiscussionsClient(new HttpClient(handler));

    var threads = await client.SearchAsync(
        ["https://example.com/a", "not a url"],
        TestContext.Current.CancellationToken);
    var thread = Assert.Single(threads);
    Assert.Equal("123", thread.ObjectId);
    Assert.Equal("hn.algolia.com", handler.Requests[0]!.Host);
    Assert.Equal("/api/v1/search", handler.Requests[0]!.AbsolutePath);

    Assert.Empty(await client.SearchAsync("https://example.com/a", TestContext.Current.CancellationToken));
  }

  [Fact]
  public void ConstructorRejectsNullHttpClient()
  {
    Assert.Throws<ArgumentNullException>(() => new HnDiscussionsClient(null!));
  }

  private sealed class QueueHandler : HttpMessageHandler
  {
    private readonly Queue<(string Body, HttpStatusCode Status)> responses;

    public QueueHandler(params (string Body, HttpStatusCode Status)[] responses) =>
        this.responses = new(responses);

    public List<Uri?> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Requests.Add(request.RequestUri);
      var response = responses.Dequeue();
      return Task.FromResult(new HttpResponseMessage(response.Status)
      {
        Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      });
    }
  }
}
