using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
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
  [InlineData(true, "1,000+")]
  [InlineData(false, "1,000")]
  [InlineData(null, "1,000")]
  public async Task OwnRowsRenderCapFlagsAndDisappearOnPublicNavigation(bool? capped, string count)
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
        PrivateCount: new Dictionary<string, int> { ["posts_saved"] = 1000 },
        PrivateCountCapped: capped is bool value ? new Dictionary<string, bool> { ["posts_saved"] = value } : null);
    using var model = new ProfileViewModel(settings,
        DispatchProxy.Create<ILandingPagesService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        DispatchProxy.Create<IPostsService, ProfilePageLocalAvatarPreviewTests.PostsProxy>(),
        DispatchProxy.Create<IImageUploadService, ProfilePageLocalAvatarPreviewTests.UnusedService>(),
        new AppConfig(new Uri("https://api.test")), localization, controller);
    var page = new ProfilePage { BindingContext = model };
    var rows = page.FindByName<VerticalStackLayout>("PrivateMetrics");
    Assert.False(rows.IsVisible);

    await model.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.True(rows.IsVisible);
    var row = Assert.IsType<Grid>(Assert.Single(rows.Children));
    Assert.Equal("Saved Posts", row.Children.OfType<Label>().First().Text);
    Assert.Equal(count, row.Children.OfType<Label>().Last().Text);
    controller.ApplySavedLocale("es");
    row = Assert.IsType<Grid>(Assert.Single(rows.Children));
    Assert.Equal(capped == true ? "1.000+" : "1.000", row.Children.OfType<Label>().Last().Text);

    await model.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(rows.IsVisible);
    Assert.Empty(rows.Children);
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
