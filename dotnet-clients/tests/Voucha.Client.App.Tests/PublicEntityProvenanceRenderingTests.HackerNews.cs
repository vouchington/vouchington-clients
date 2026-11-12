using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public void PostAndProfileCardsRenderCompactStoredReferences()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    Application.Current!.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var posts = new PostsListViewModel(Proxy<IPostsService>());
    var page = new PostsPage(posts, new AnonymousSessionStore(), null!, Recovery(locale));
    var row = PostRows.From(new Post("post", "discussion", "Title", null, null), null, null, null) with
    {
      HackerNewsReferences = new(2, 1),
    };
    AssertCompactTemplate(page, row);
    using var profile = new ProfileViewModel(Proxy<ISettingsService>(), Proxy<ILandingPagesService>(),
        Proxy<IPostsService>(), Proxy<IImageUploadService>(), new AppConfig(new Uri("https://api.test")));
    AssertCompactTemplate(new ProfilePage { BindingContext = profile }, row);
  }

  [Theory]
  [InlineData(NewsFeedKind.News, NewsFeedScope.AllNews)]
  [InlineData(NewsFeedKind.Podcasts, NewsFeedScope.AllPodcasts)]
  public void NewsAndPodcastCardsRenderCompactStoredReferences(NewsFeedKind kind, NewsFeedScope scope)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var model = new NewsFeedsViewModel(Proxy<INewsFeedService>(), kind, scope);
    var page = new NewsFeedsPage(model, new AnonymousSessionStore(), null!, Recovery(locale));
    var item = new NewsFeedItem("item", "Title", "Source", "Summary", null, DateTimeOffset.UnixEpoch,
        kind == NewsFeedKind.News ? NewsFeedItemKind.Article : NewsFeedItemKind.Media, HackerNewsReferences: new(2, 1));
    AssertCompactTemplate(page, item);
  }

  [Fact]
  public void RelatedArticleCardsRenderCompactStoredReferences()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var peer = new NewsFeedItem("peer", "Title", "Source", "Summary", null, DateTimeOffset.UnixEpoch,
        HackerNewsReferences: new(2, 1));
    var related = new StoryRelatedArticles("story", "primary", [peer], new PageInfo(null, false, null), UiLocalization.English)
    {
      IsExpanded = true,
    };
    var primary = peer with { Id = "primary", StoryArticles = related };
    var view = new StoryRelatedArticlesView { PrimaryItem = primary };
    var control = Assert.Single(VisualTreeElements.Descendants<HackerNewsReferencesView>(view));
    Assert.True(control.IsVisible);
    Assert.False(control.ShowDetails);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", Assert.IsType<Label>(Assert.Single(control.Children)).Text);
  }

  private static void AssertCompactTemplate<T>(Page page, T row)
  {
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view => view.ItemsSource is IReadOnlyList<T>);
    var card = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    card.BindingContext = row;
    var control = Assert.Single(VisualTreeElements.Descendants<HackerNewsReferencesView>(card));
    Assert.False(control.ShowDetails);
    Assert.True(control.IsVisible);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", Assert.IsType<Label>(Assert.Single(control.Children)).Text);
  }
}
