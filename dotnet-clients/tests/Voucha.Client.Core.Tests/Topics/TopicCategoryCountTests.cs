using System.Net;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicCategoryCountTests
{
  [Theory]
  [InlineData("web.topics.show.viewer-lower-bound", "Posts (0+)", "Reviews (0)", "Data Points (0)")]
  [InlineData("web.topics.show.viewer-review-data-lower-bound", "Posts (0+)", "Reviews (0+)", "Data Points (0+)")]
  public async Task FixtureCountsRemainIndependentAndUsePublicCounts(string fixture, string posts, string reviews, string data)
  {
    var json = JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixture))!;
    using var model = Model(json.ToJsonString());
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    Assert.Equal(posts, model.PostsCountLabel);
    Assert.Equal(reviews, model.ReviewsCountLabel);
    Assert.Equal(data, model.DataPointsCountLabel);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MissingOrNullCapFlagsRetainPublicCountsAndDetectAdditionalViewerResults(bool nullFlags)
  {
    var json = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.topics.show.viewer-lower-bound"))!;
    if (nullFlags) json["topic_metrics"]!["viewer_count_capped"] = null;
    else json["topic_metrics"]!.AsObject().Remove("viewer_count_capped");
    json["topic_metrics"]!["count"] = new JsonObject { ["reviews"] = 1234 };
    json["topic_metrics"]!["viewer_count"] = new JsonObject { ["reviews"] = 1235 };
    using var model = Model(json.ToJsonString());
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    Assert.Equal("Posts (1,234+)", model.PostsCountLabel);
    Assert.Equal("Reviews (1,234+)", model.ReviewsCountLabel);
    Assert.Equal("Data Points (0)", model.DataPointsCountLabel);
  }

  [Theory]
  [InlineData(true, false, 2, "Posts (6+)")]
  [InlineData(false, true, 2, "Posts (6+)")]
  [InlineData(false, false, 3, "Posts (6+)")]
  [InlineData(false, false, 2, "Posts (6)")]
  [InlineData(false, false, 1, "Posts (6)")]
  public async Task PublicCountIsDisplayedWithOnlyItsOwnLowerBound(bool publicCap, bool viewerCap, int viewerCount, string expected)
  {
    var json = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.topics.show.viewer-lower-bound"))!;
    json["topic_metrics"] = new JsonObject
    {
      ["count"] = new JsonObject { ["discussions"] = 2, ["reviews"] = 4 },
      ["viewer_count"] = new JsonObject { ["discussions"] = viewerCount, ["reviews"] = 4 },
      ["count_capped"] = new JsonObject { ["discussions"] = publicCap },
      ["viewer_count_capped"] = new JsonObject { ["discussions"] = viewerCap },
    };
    using var model = Model(json.ToJsonString());
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    Assert.Equal(expected, model.PostsCountLabel);
    Assert.Equal("Reviews (4)", model.ReviewsCountLabel);
    Assert.Equal("Data Points (0)", model.DataPointsCountLabel);
  }

  [Theory]
  [InlineData(0, "Posts (6)")]
  [InlineData(1, "Posts (6)")]
  [InlineData(2, "Posts (6+)")]
  public async Task PostsComparesAggregateViewerAndPublicCounts(int reviewViewer, string expected)
  {
    var json = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.topics.show.viewer-lower-bound"))!;
    json["topic_metrics"] = new JsonObject
    {
      ["count"] = new JsonObject { ["discussions"] = 2, ["reviews"] = 4 },
      ["viewer_count"] = new JsonObject { ["discussions"] = 5, ["reviews"] = reviewViewer },
    };
    using var model = Model(json.ToJsonString());
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    Assert.Equal(expected, model.PostsCountLabel);
    Assert.Equal("Reviews (4)", model.ReviewsCountLabel);
  }

  [Fact]
  public async Task MissingMetricsAndLocaleChangesRefreshAllLabels()
  {
    using var locale = new UiLocaleController(new EnglishProvider());
    var json = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.topics.show.viewer-lower-bound"))!;
    json.AsObject().Remove("topic_metrics");
    using var model = Model(json.ToJsonString(), locale);
    Assert.Equal("Posts (0)", model.PostsCountLabel);
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    var changed = new List<string?>();
    model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
    locale.ApplySavedLocale("es");
    Assert.Contains(nameof(model.PostsCountLabel), changed);
    Assert.Contains(nameof(model.ReviewsCountLabel), changed);
    Assert.Contains(nameof(model.DataPointsCountLabel), changed);
    Assert.NotEqual("Posts (0)", model.PostsCountLabel);
  }

  private static TopicDetailViewModel Model(string json, UiLocaleController? locale = null) => new(
      new ApiTopicsService(new VouchaApiClient(new HttpClient(new ResponseHandler(json)) { BaseAddress = new Uri("https://api.test") })),
      localization: locale is null ? null : new UiLocalization(locale), localeController: locale);

  private sealed class EnglishProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class ResponseHandler(string json) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
  }
}
