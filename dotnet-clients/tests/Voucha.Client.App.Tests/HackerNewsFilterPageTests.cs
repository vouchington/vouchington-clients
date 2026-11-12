using System.ComponentModel;
using System.Net;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PostFilterButtonReloadsCurrentModeAndTypeAndRelocalizes(bool authenticated)
  {
    PrepareHackerNewsFilters();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(locale);
    using var copy = UiCopy.PushLocalization(localization);
    var handler = new FilterHandler("""{"results":[],"posts":{},"users":{},"communities":{},"page_info":{"has_next_page":false}}""");
    using var model = new PostsListViewModel(new ApiPostsService(new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })), localization, locale);
    var page = new PostsPage(model, new FilterSession(authenticated), null!, Recovery(locale));
    var binding = Assert.IsType<PostsPageBinding>(page.BindingContext);
    var filter = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == model.HackerNewsFilterLabel);
    var feed = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == localization.Localize(UiMessageKey.NativeDotnetPostsFeed));
    var all = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == localization.Localize(UiMessageKey.NativeSwiftCommonAll));
    await ClickAndLoadAsync(feed, model, () => model.IsLoading);
    Assert.Equal(!authenticated, filter.IsVisible);
    await ClickAndLoadAsync(all, model, () => model.IsLoading);
    Assert.True(filter.IsVisible);
    Assert.True(binding.IsHackerNewsFilterVisible);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.True(binding.IsHackerNewsFilterSelected);
    Assert.Contains("has_hacker_news_references=true", handler.Paths[^1], StringComparison.Ordinal);
    Assert.StartsWith("/api/v1/posts?", handler.Paths[^1], StringComparison.Ordinal);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.False(binding.IsHackerNewsFilterSelected);
    Assert.DoesNotContain("has_hacker_news_references", handler.Paths[^1], StringComparison.Ordinal);
    var reviews = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == localization.Localize(UiMessageKey.NativeSwiftNavigationTitlesReviews));
    await ClickAndLoadAsync(reviews, model, () => model.IsLoading);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.Contains("post_types=review", handler.Paths[^1], StringComparison.Ordinal);
    Assert.StartsWith("/api/v1/posts?", handler.Paths[^1], StringComparison.Ordinal);
    await ClickAndLoadAsync(feed, model, () => model.IsLoading);
    Assert.False(binding.IsHackerNewsFilterSelected);
    Assert.Equal(!authenticated, filter.IsVisible);
    await ClickAndLoadAsync(all, model, () => model.IsLoading);
    Assert.True(filter.IsVisible);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.True(binding.IsHackerNewsFilterSelected);
    Assert.StartsWith("/api/v1/posts?", handler.Paths[^1], StringComparison.Ordinal);
    Assert.Contains("has_hacker_news_references=true", handler.Paths[^1], StringComparison.Ordinal);
    var english = filter.Text;
    locale.ApplySavedLocale("es");
    Assert.NotEqual(english, filter.Text);
    Assert.Equal(localization.Localize(UiMessageKey.SharedHackerNewsFilterLabel), filter.Text);
  }

  [Theory]
  [InlineData(NewsFeedKind.News, NewsFeedScope.AllNews, "article")]
  [InlineData(NewsFeedKind.Podcasts, NewsFeedScope.AllPodcasts, "audio")]
  [InlineData(NewsFeedKind.News, NewsFeedScope.YourFeed, "article")]
  [InlineData(NewsFeedKind.Podcasts, NewsFeedScope.YourPodcasts, "audio")]
  public async Task FeedFilterButtonReloadsItsMediaScopeHidesForSourcesAndRelocalizes(NewsFeedKind kind, NewsFeedScope scope, string media)
  {
    PrepareHackerNewsFilters();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(locale);
    using var copy = UiCopy.PushLocalization(localization);
    var handler = new FilterHandler("""{"results":[],"rss_feed_items":{},"rss_feeds":{},"page_info":{"has_next_page":false}}""");
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })), kind, scope,
        localization: localization, localeController: locale);
    var page = new NewsFeedsPage(model, new FilterSession(false), null!, Recovery(locale));
    var binding = Assert.IsType<NewsFeedsPageBinding>(page.BindingContext);
    var filter = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == model.HackerNewsFilterLabel);
    Assert.True(filter.IsVisible);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.True(binding.IsHackerNewsFilterSelected);
    Assert.Contains($"media_type={media}", handler.Paths[^1], StringComparison.Ordinal);
    Assert.Contains("has_hacker_news_references=true", handler.Paths[^1], StringComparison.Ordinal);
    await ClickAndLoadAsync(filter, model, () => model.IsLoading);
    Assert.False(binding.IsHackerNewsFilterSelected);
    Assert.DoesNotContain("has_hacker_news_references", handler.Paths[^1], StringComparison.Ordinal);
    var english = filter.Text;
    locale.ApplySavedLocale("es");
    Assert.NotEqual(english, filter.Text);
    Assert.Equal(localization.Localize(UiMessageKey.SharedHackerNewsFilterLabel), filter.Text);
    await model.SelectScopeAsync(kind.GetAllSourcesScope(), TestContext.Current.CancellationToken);
    Assert.False(binding.IsHackerNewsFilterVisible);
    Assert.False(filter.IsVisible);
    if (kind == NewsFeedKind.News)
      await model.SelectSourceFeedTypeAsync(NewsFeedSourceType.Podcast, TestContext.Current.CancellationToken);
  }

  private static void PrepareHackerNewsFilters()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    foreach (var key in UiMessageKey.All)
      Application.Current!.Resources[key.Value] = UiLocalization.English.Localize(key);
  }

  private static async Task ClickAndLoadAsync(Button button, INotifyPropertyChanged model, Func<bool> loading)
  {
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = false;
    void Changed(object? sender, PropertyChangedEventArgs args)
    {
      if (loading()) started = true;
      else if (started) completed.TrySetResult();
    }
    model.PropertyChanged += Changed;
    try
    {
      button.SendClicked();
      await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
    finally { model.PropertyChanged -= Changed; }
  }

  private sealed class FilterHandler(string json) : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
  }

  private sealed class FilterSession(bool authenticated) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => authenticated ? new(new User("viewer", "viewer")) : SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
