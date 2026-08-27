using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.HnDiscussions;
using Xunit;

namespace Voucha.Client.Core.Tests.HnDiscussions;

public sealed class HnDiscussionsViewModelTests
{
  [Fact]
  public void MemoryAndSessionSettingsExposeTheIdentityFlag()
  {
    Assert.False(new MemoryHnDiscussionsSettings().Enabled);
    Assert.True(new MemoryHnDiscussionsSettings(true).Enabled);
    Assert.False(new SessionHnDiscussionsSettings(new TestSessionStore(null)).Enabled);
    Assert.True(
        new SessionHnDiscussionsSettings(
            new TestSessionStore(new User("user-1", "alice", HnDiscussions: true))).Enabled);
  }

  [Fact]
  public async Task LoadAsyncSkipsSearchWhenThePreferenceIsOff()
  {
    var handler = new QueueHandler(("{}", HttpStatusCode.OK));
    var viewModel = new HnDiscussionsViewModel(
        new MemoryHnDiscussionsSettings(),
        new HnDiscussionsClient(new HttpClient(handler)));

    await viewModel.LoadAsync(["https://example.com/a"], TestContext.Current.CancellationToken);

    Assert.Empty(handler.Requests);
    Assert.False(viewModel.HasThreads);
    Assert.NotEmpty(viewModel.LocalizedHeading);
  }

  [Fact]
  public async Task LoadAsyncFromPostRendersTitleScoreAndComments()
  {
    const string json = """
        {"hits":[{"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18}]}
        """;
    var handler = new QueueHandler((json, HttpStatusCode.OK));
    var viewModel = new HnDiscussionsViewModel(
        new MemoryHnDiscussionsSettings(true),
        new HnDiscussionsClient(new HttpClient(handler)));

    await viewModel.LoadAsync(
        new Post(
            "post-1",
            "post",
            "Title",
            "See https://example.com/a.",
            "user-1",
            Url: new Uri("https://example.com/a")),
        TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Threads);
    Assert.Equal("Example", row.Title);
    Assert.Contains("42", row.LocalizedMetadata, StringComparison.Ordinal);
    Assert.Contains("18", row.LocalizedMetadata, StringComparison.Ordinal);
    Assert.Equal(new Uri("https://news.ycombinator.com/item?id=123"), row.ItemUrl);
    Assert.True(viewModel.HasThreads);
  }

  [Fact]
  public async Task SearchAsyncReturnsEmptyWhenAlgoliaIsUnreachable()
  {
    var client = new HnDiscussionsClient(new HttpClient(new ThrowingHandler()));
    Assert.Empty(await client.SearchAsync("https://example.com/a", TestContext.Current.CancellationToken));
    Assert.Empty(
        await client.SearchAsync(
            ["https://example.com/a", "https://example.com/a"],
            TestContext.Current.CancellationToken));
  }

  private sealed class TestSessionStore(User? identity) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = new(identity);

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class QueueHandler((string Body, HttpStatusCode Status) response) : HttpMessageHandler
  {
    public List<Uri?> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Requests.Add(request.RequestUri);
      return Task.FromResult(new HttpResponseMessage(response.Status)
      {
        Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      });
    }
  }

  private sealed class ThrowingHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new HttpRequestException("offline");
  }
}
