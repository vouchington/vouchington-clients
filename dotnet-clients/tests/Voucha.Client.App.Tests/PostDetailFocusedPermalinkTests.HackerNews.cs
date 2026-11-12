using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PostDetailFocusedPermalinkTests
{
  [Fact]
  public async Task RootStoredHackerNewsReferencesRenderFullLinksAndClearOnReload()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new StubDeviceLanguageProvider("en")));
    var root = Post("root", null);
    var references = new HackerNewsReferences(1, 1,
        [new(303, 301, DateTimeOffset.UnixEpoch)], [new(301, 42, DateTimeOffset.UnixEpoch, 7)]);
    var service = new ThreadService(new PostResponse(root,
        PostHackerNewsReferences: new Dictionary<string, HackerNewsReferences> { [root.Id] = references }), Response([]), Response([]))
    {
      NextRoot = new PostResponse(root),
    };
    var binding = new PostDetailPageBinding(new CommentThreadViewModel(service, root.Id), new SessionStore(SessionSnapshot.Anonymous));
    var page = new PostDetailPage { BindingContext = binding };
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    var control = Assert.Single(page.GetVisualTreeDescendants().OfType<HackerNewsReferencesView>());
    Assert.True(control.ShowDetails);
    Assert.True(control.IsVisible);
    Assert.Equal(2, control.Children.OfType<Button>().Count());
    Assert.Same(references, binding.RootRow!.HackerNewsReferences);
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.False(control.IsVisible);
    Assert.Empty(control.Children);
  }
}
