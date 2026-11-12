using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class RssFeedDeletionTests
{
  private const string FeedId = "70000000-0000-7000-8000-000000003680";

  [Fact]
  public async Task ProducerAcceptedAcknowledgementMeansPendingWithoutPollingOrRemoving()
  {
    var handler = new Handler(HttpStatusCode.Accepted, Fixture("accepted"));
    using var http = Http(handler);
    var result = await new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken);
    Assert.True(result.IsAcceptedPending);
    Assert.Equal("deleting", result.Status);
    Assert.Equal(FeedId, result.RssFeedId);
    Assert.Equal(UiMessageKey.SharedRssFeedDeletionAccepted, result.Message.Key);
    Assert.Equal([("DELETE", $"/api/v1/rss-feeds/{FeedId}", (string?)null)], handler.Requests);
  }

  [Fact]
  public async Task CanonicalResponseIdIsPreservedWithoutComparingRequestSpelling()
  {
    var canonical = "aaaaaaaa-0000-7000-8000-000000003680";
    var body = JsonNode.Parse(Fixture("accepted"))!.AsObject();
    body["rss_feed_id"] = canonical;
    var handler = new Handler(HttpStatusCode.Accepted, body.ToJsonString());
    using var http = Http(handler);
    var result = await new VouchaApiClient(http).DeleteRssFeedAsync(canonical.ToUpperInvariant(), TestContext.Current.CancellationToken);
    Assert.True(result.IsAcceptedPending);
    Assert.Equal("deleting", result.Status);
    Assert.Equal(canonical, result.RssFeedId);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ProducerConflictMatchesCodeEvenWhenServerMessageChanges(bool changeMessage)
  {
    var body = JsonNode.Parse(Fixture("retained-import-conflict"))!.AsObject();
    if (changeMessage) body["message"] = "Different server text";
    var handler = new Handler(HttpStatusCode.Conflict, body.ToJsonString());
    using var http = Http(handler);
    var result = await new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken);
    Assert.False(result.IsAcceptedPending);
    Assert.Null(result.Status);
    Assert.Null(result.RssFeedId);
    Assert.Equal(UiMessageKey.SharedRssFeedDeletionBlockedByAdminImports, result.Message.Key);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData("unknown", 409)]
  [InlineData(null, 409)]
  [InlineData("RSS_FEED_ADMIN_IMPORT_REFERENCED", 403)]
  [InlineData("RSS_FEED_ADMIN_IMPORT_REFERENCED", 500)]
  public async Task SameServerMessageWithoutExactConflictCodeIsANormalApiError(string? code, int status)
  {
    var body = JsonNode.Parse(Fixture("retained-import-conflict"))!.AsObject();
    if (code is null) body.Remove("code");
    else body["code"] = code;
    var json = body.ToJsonString();
    var handler = new Handler((HttpStatusCode)status, json);
    using var http = Http(handler);
    var error = await Assert.ThrowsAsync<VouchaApiException>(() =>
        new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken));
    Assert.Equal((HttpStatusCode)status, error.StatusCode);
    Assert.Equal(code, error.ErrorCode);
    Assert.Equal(json, error.ResponseBody);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task EmptyErrorKeepsTheNormalApiErrorShape()
  {
    var handler = new Handler(HttpStatusCode.Conflict, string.Empty);
    using var http = Http(handler);
    var error = await Assert.ThrowsAsync<VouchaApiException>(() =>
        new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    Assert.Null(error.ResponseBody);
    Assert.Null(error.ErrorCode);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData("null")]
  [InlineData("{}")]
  [InlineData("invalid")]
  [InlineData("")]
  [InlineData("{\"rss_feed_id\":null,\"status\":\"deleting\"}")]
  [InlineData("{\"rss_feed_id\":\"\",\"status\":\"deleting\"}")]
  [InlineData("{\"rss_feed_id\":\"70000000-0000-7000-8000-000000003680\",\"status\":\"deleted\"}")]
  [InlineData("{\"rss_feed_id\":\"70000000-0000-7000-8000-000000003680\",\"status\":null}")]
  [InlineData("{\"rss_feed_id\":12,\"status\":\"deleting\"}")]
  public async Task Malformed202AcknowledgementsAreNotAccepted(string json)
  {
    var handler = new Handler(HttpStatusCode.Accepted, json);
    using var http = Http(handler);
    var error = await Assert.ThrowsAsync<VouchaApiDecodeException>(() =>
        new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Accepted, error.StatusCode);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(200)]
  [InlineData(201)]
  [InlineData(204)]
  public async Task MatchingBodyWithAnotherSuccessStatusIsNotAccepted(int status)
  {
    var handler = new Handler((HttpStatusCode)status, Fixture("accepted"));
    using var http = Http(handler);
    var error = await Assert.ThrowsAsync<VouchaApiDecodeException>(() =>
        new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, TestContext.Current.CancellationToken));
    Assert.Equal((HttpStatusCode)status, error.StatusCode);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NetworkFailureOrCancellationNeverRetries(bool cancel)
  {
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var handler = new Handler(HttpStatusCode.Accepted, Fixture("accepted")) { Cancel = cancel, Cancellation = cancellation };
    using var http = Http(handler);
    var error = await Record.ExceptionAsync(() => new VouchaApiClient(http).DeleteRssFeedAsync(FeedId, cancellation.Token));
    if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
    else Assert.IsType<HttpRequestException>(error);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public void ProducerAcknowledgementIsTypedWithoutDiscardingItsStatus()
  {
    var acknowledgement = JsonSerializer.Deserialize<RssFeedDeletionAcknowledgement>(Fixture("accepted"), VouchaApiJson.Options)!;
    Assert.Equal(FeedId, acknowledgement.RssFeedId);
    Assert.Equal("deleting", acknowledgement.Status);
  }

  private static string Fixture(string suffix) => ApiFixtureLoader.LoadResponse($"native.rss-feeds.delete.{suffix}");
  private static HttpClient Http(Handler handler) => new(handler) { BaseAddress = new("https://api.test") };

  private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
  {
    public List<(string Method, string Path, string? Body)> Requests { get; } = [];
    public bool? Cancel { get; init; }
    public CancellationTokenSource? Cancellation { get; init; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery,
          request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
      if (Cancel is false) throw new HttpRequestException("Ambiguous network failure");
      if (Cancel is true)
      {
        Cancellation!.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
      }
      return new(status) { Content = new StringContent(body), RequestMessage = request };
    }
  }
}
