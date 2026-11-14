using System.Net;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task PostsPageDropsItsSelectionWhenTheFlagTurnsOffSoALaterLoadDoesNotReselectIt()
  {
    PrepareHackerNewsFilters();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(locale);
    using var copy = UiCopy.PushLocalization(localization);
    var handler = new SwitchableFilterHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var gate = new HackerNewsReferenceGate(client.FetchHackerNewsConfigAsync);
    using var model = new PostsListViewModel(new ApiPostsService(client), localization, locale, gate);
    var page = new PostsPage(model, new FilterSession(false), null!, Recovery(locale));
    var binding = Assert.IsType<PostsPageBinding>(page.BindingContext);
    var filter = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == model.HackerNewsFilterLabel);
    var all = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == localization.Localize(UiMessageKey.NativeSwiftCommonAll));
    await ClickAndLoadAsync(all, model, () => model.IsLoading);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.True(binding.IsHackerNewsFilterSelected);

    handler.Visible = false;
    await ActAndLoadAsync(() => _ = gate.RefreshAsync(TestContext.Current.CancellationToken), model, () => model.IsLoading);
    Assert.False(binding.IsHackerNewsFilterSelected);
    Assert.DoesNotContain("has_hacker_news_references", handler.Paths[^1], StringComparison.Ordinal);

    handler.Visible = true;
    await ClickAndLoadAsync(all, model, () => model.IsLoading);
    Assert.False(binding.IsHackerNewsFilterSelected);
    Assert.DoesNotContain("has_hacker_news_references", handler.Paths[^1], StringComparison.Ordinal);

    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.True(binding.IsHackerNewsFilterSelected);
    Assert.Contains("has_hacker_news_references=true", handler.Paths[^1], StringComparison.Ordinal);
  }

  private sealed class SwitchableFilterHandler : HttpMessageHandler
  {
    public volatile bool Visible = true;
    public List<string> Paths { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var json = request.RequestUri!.AbsolutePath == "/api/v1/hacker-news-config"
          ? $$"""{"references_visible":{{(Visible ? "true" : "false")}},"post_time_range_cap":"1y"}"""
          : """{"results":[],"posts":{},"users":{},"communities":{},"page_info":{"has_next_page":false}}""";
      if (request.RequestUri.AbsolutePath != "/api/v1/hacker-news-config") Paths.Add(request.RequestUri.PathAndQuery);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
  }
}
