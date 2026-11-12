using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SourceCreation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.SourceCreation;

public sealed class SourceCreationServiceTests
{
  [Fact]
  public async Task CanonicalHttpFlowUsesOnePostAndAuthenticatedClientForReads()
  {
    var methods = new List<HttpMethod>();
    using var handler = new Handler(async request =>
    {
      methods.Add(request.Method);
      Assert.Equal("api.test", request.RequestUri!.Host);
      Assert.Contains("st=session", request.Headers.GetValues("Cookie"));
      if (request.Method == HttpMethod.Post)
      {
        Assert.Equal("/api/v1/rss-feeds", request.RequestUri.AbsolutePath);
        Assert.Contains("https://example.test/feed.xml", await request.Content!.ReadAsStringAsync());
        return new(HttpStatusCode.Accepted) { Content = new StringContent(ApiFixtureLoader.LoadResponse("native.rss-feeds.create.accepted")) };
      }
      if (request.RequestUri.AbsolutePath.Contains("/my/import/", StringComparison.Ordinal))
        return new(HttpStatusCode.OK) { Content = new StringContent(ApiFixtureLoader.LoadResponse("native.rss-feeds.create.status.completed")) };
      Assert.EndsWith(SourceCreationTests.Status("completed").Rows[0].EntityId!, request.RequestUri.AbsolutePath, StringComparison.Ordinal);
      // Same narrow rss_feed/ topic_id response shape consumed by Swift RssFeedDetailResponse.
      return new(HttpStatusCode.OK) { Content = new StringContent("{\"rss_feed\":{\"id\":\"" + SourceCreationTests.Status("completed").Rows[0].EntityId + "\",\"topic_id\":\"topic\"}}") };
    });
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    http.DefaultRequestHeaders.Add("Cookie", "st=session");
    var service = new SourceCreationService(new(http), http);
    using var model = new SourceCreationViewModel(service, new SourceCreationTests.Session(), UiLocalization.English);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Get }, methods);
    Assert.True(model.CanOpen);
    Assert.Equal("topic", model.TopicSlug);

  }

  [Fact]
  public async Task ChangedApiCannotResumeOldImport()
  {
    var service = new SourceCreationTests.FakeService { DefaultStatus = SourceCreationTests.Status("pending") };
    using var model = SourceCreationTests.Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    service.ApiOrigin = new("https://other.test");
    await model.RetryAsync();
    Assert.True(model.CanCreate);
    Assert.False(model.CanRetry);
    Assert.Equal(30, service.Gets);
  }

  [Fact]
  public async Task CancellationDuringGetDoesNotPublishFailure()
  {
    var service = new SourceCreationTests.FakeService();
    var pending = new TaskCompletionSource<RssFeedImportStatus>();
    service.Reads.Enqueue(() => pending.Task);
    using var model = SourceCreationTests.Model(service);
    model.Input = "https://example.test/feed.xml";
    var operation = model.CreateAsync();
    model.Cancel();
    pending.SetException(new OperationCanceledException());
    await operation;
    Assert.Null(model.Error);
    Assert.Null(model.EntityId);
  }

  private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
  }
}
