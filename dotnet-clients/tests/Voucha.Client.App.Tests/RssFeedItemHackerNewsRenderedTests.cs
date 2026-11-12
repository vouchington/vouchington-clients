using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class RssFeedItemHackerNewsRenderedTests
{
  [Theory]
  [InlineData(NewsFeedItemKind.Article)]
  [InlineData(NewsFeedItemKind.Media)]
  public async Task ArticleAndEpisodeDetailsRenderStoredReferencesAndRefreshAfterReload(NewsFeedItemKind kind)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var session = new SessionStore(SessionSnapshot.Anonymous);
    using var services = new ServiceCollection()
        .AddSingleton<ISessionStore>(session)
        .AddSingleton<IProductSurfaceAnalytics>(new SessionProductSurfaceAnalytics(new VouchaApiClient(new HttpClient()), session))
        .BuildServiceProvider();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var references = new HackerNewsReferences(1, 1,
        [new(303, 301, DateTimeOffset.UnixEpoch)], [new(301, 42, DateTimeOffset.UnixEpoch, 7)]);
    var service = new DetailService(new("item", "Title", "Source", "Summary", null, DateTimeOffset.UnixEpoch,
        kind, HackerNewsReferences: references));
    var localization = new UiLocalization(locale);
    var page = new RssFeedItemDetailPage(service, services, "item", kind, localization, locale);
    var model = Assert.IsType<RssFeedItemDetailViewModel>(page.BindingContext);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var control = Assert.Single(page.GetVisualTreeDescendants().OfType<HackerNewsReferencesView>());
    Assert.True(control.ShowDetails);
    Assert.True(control.IsVisible);
    Assert.Equal(2, control.Children.OfType<Button>().Count());
    var english = Assert.IsType<Label>(control.Children[0]).Text;
    locale.ApplySavedLocale("es");
    Assert.NotEqual(english, Assert.IsType<Label>(control.Children[0]).Text);
    service.Item = service.Item with { HackerNewsReferences = null };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(control.IsVisible);
    Assert.Empty(control.Children);
  }

  private sealed class SessionStore(SessionSnapshot current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => current;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DetailService(NewsFeedItem item) : IRssFeedItemDetailService
  {
    public NewsFeedItem Item { get; set; } = item;
    public Task<RssFeedItemDetail> FetchAsync(string itemId, NewsFeedItemKind kind, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RssFeedItemDetail(Item, null));
  }
}
