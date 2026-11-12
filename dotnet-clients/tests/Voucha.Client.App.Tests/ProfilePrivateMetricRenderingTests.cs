using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ProfilePrivateMetricRenderingTests
{
  [Theory]
  [InlineData("posts_saved", true, "1,000+")]
  [InlineData("posts_saved", false, "1,000")]
  [InlineData("posts_saved", null, "1,000")]
  [InlineData("rss_feed_items_hidden", true, "1,000+")]
  [InlineData("rss_feed_items_hidden", false, "1,000")]
  [InlineData("rss_feed_items_hidden", null, "1,000")]
  public async Task OwnRowsRenderCapFlagsAndDisappearOnPublicNavigation(string key, bool? capped, string count)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(localization);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(controller);
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    var settings = DispatchProxy.Create<ISettingsService, MetricSettingsProxy>();
    ((MetricSettingsProxy)(object)settings).Metrics = new UserMetrics("user_metrics", "user-1", new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int> { [key] = 1000 },
        PrivateCountCapped: capped is bool value ? new Dictionary<string, bool> { [key] = value } : null);
    using var model = new ProfileViewModel(settings,
        DispatchProxy.Create<ILandingPagesService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        DispatchProxy.Create<IPostsService, ProfilePageLocalAvatarPreviewTests.PostsProxy>(),
        DispatchProxy.Create<IImageUploadService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        new AppConfig(new Uri("https://api.test")), localization, controller);
    var page = new ProfilePage(model);
    var rows = page.FindByName<VerticalStackLayout>("PrivateMetrics");
    Assert.False(rows.IsVisible);

    model.SetCurrentViewer("user-1", "alice");
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.True(rows.IsVisible);
    var row = Assert.IsType<Grid>(Assert.Single(rows.Children));
    Assert.Equal(Assert.Single(model.PrivateMetricRows).Label, row.Children.OfType<Label>().First().Text);
    Assert.Equal(count, row.Children.OfType<Label>().Last().Text);
    controller.ApplySavedLocale("es");
    row = Assert.IsType<Grid>(Assert.Single(rows.Children));
    Assert.Equal(capped == true ? "1.000+" : "1.000", row.Children.OfType<Label>().Last().Text);

    await model.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(rows.IsVisible);
    Assert.Empty(rows.Children);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("user-2")]
  public async Task SessionCallbackRemovesCachedPrivateRowsOnLogoutOrAccountSwitch(string? nextUserId)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new StubDeviceLanguageProvider("en")));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    var settings = DispatchProxy.Create<ISettingsService, MetricSettingsProxy>();
    ((MetricSettingsProxy)(object)settings).Metrics = new UserMetrics("user_metrics", "user-1", new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int> { ["rss_feed_items_hidden"] = 1000 });
    using var model = new ProfileViewModel(settings,
        DispatchProxy.Create<ILandingPagesService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        DispatchProxy.Create<IPostsService, ProfilePageLocalAvatarPreviewTests.PostsProxy>(),
        DispatchProxy.Create<IImageUploadService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        new AppConfig(new Uri("https://api.test")));
    var session = new MetricSessionStore();
    var page = new ProfilePage(model);
    page.AttachMetricSession(session);
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);
    var rows = page.FindByName<VerticalStackLayout>("PrivateMetrics");
    Assert.True(rows.IsVisible);
    Assert.Single(rows.Children);

    session.ChangeIdentity(nextUserId);
    await page.SessionRefreshTask;

    Assert.False(rows.IsVisible);
    Assert.Empty(rows.Children);
    Assert.Empty(model.PrivateMetricRows);
  }

  private sealed class MetricSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("user-1", "alice"));
    public void ChangeIdentity(string? userId)
    {
      Current = new SessionSnapshot(userId is null ? null : new User(userId, "next"));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  public class MetricSettingsProxy : DispatchProxy
  {
    public UserMetrics? Metrics { get; set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ISettingsService.FetchMyIdentityAsync) => Task.FromResult(new MyIdentityResponse(new User("user-1", "alice"))),
      nameof(ISettingsService.FetchUserAsync) => Task.FromResult(new UserResponse(new User("user-1", "alice"), Metrics)),
      _ => throw new InvalidOperationException($"Unexpected settings call: {targetMethod?.Name}"),
    };
  }
}
