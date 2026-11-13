using System.Net;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task TopicHackerNewsToggleResetsYearPickerKeepsMonthAndCursorAndRestoresAllOnDisable()
  {
    PrepareHackerNewsFilters();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var handler = new TimeRangePostHandler();
    using var model = new PostsListViewModel(new ApiPostsService(new(new HttpClient(handler)
    { BaseAddress = new Uri("https://api.test") })), new UiLocalization(locale), locale);
    var page = new PostsPage(model, new FilterSession(true), null!, Recovery(locale));
    var scope = new FetchPostsRequest(PostTypes: "review", ReviewTopic: "canonical-topic", Sort: "following_new");
    page.ConfigureBrowseScope(scope, topicScope: true);
    await model.LoadBrowseAsync("review", scope: scope, cancellationToken: TestContext.Current.CancellationToken);
    var control = page.FindByName<PostTimeRangePicker>("TimeRangePicker");
    var picker = control.FindByName<Picker>("RangePicker");
    var filter = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == model.HackerNewsFilterLabel);
    Assert.Equal(1, picker.SelectedIndex);
    Assert.True(control.IsVisible);

    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.Equal(new[] { "Last 7 days", "Last 30 days", "Last year" }, picker.Items);
    Assert.Equal(2, picker.SelectedIndex);
    Assert.Contains("time_range=1y", handler.Paths[^1], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.Paths[^1], StringComparison.Ordinal);
    await ActAndLoadAsync(() => picker.SelectedIndex = 1, model, () => model.IsLoading);
    Assert.Contains("time_range=1m", handler.Paths[^1], StringComparison.Ordinal);
    Assert.Contains("has_hacker_news_references=true", handler.Paths[^1], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.Paths[^1], StringComparison.Ordinal);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Contains("after=range-cursor", handler.Paths[^1], StringComparison.Ordinal);
    Assert.Contains("time_range=1m", handler.Paths[^1], StringComparison.Ordinal);
    Assert.All(handler.Paths, path =>
    {
      Assert.Contains("review_topic=canonical-topic", path, StringComparison.Ordinal);
      Assert.Contains("post_types=review", path, StringComparison.Ordinal);
      Assert.Contains("sort=following_new", path, StringComparison.Ordinal);
    });
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.Equal(new[] { "Last 7 days", "Last 30 days", "Last year", "All time" }, picker.Items);
    Assert.Equal(1, picker.SelectedIndex);
    Assert.Contains("time_range=1m", handler.Paths[^1], StringComparison.Ordinal);
    Assert.DoesNotContain("has_hacker_news_references", handler.Paths[^1], StringComparison.Ordinal);
  }

  private sealed class TimeRangePostHandler : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent("""{"results":[],"posts":{},"users":{},"communities":{},"page_info":{"has_next_page":true,"end_cursor":"range-cursor"}}"""),
      });
    }
  }
}
