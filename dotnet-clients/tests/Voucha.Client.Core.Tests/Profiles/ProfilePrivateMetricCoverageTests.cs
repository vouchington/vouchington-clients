using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task AvailableAggregateMetricRendersWhileUnpublishedMetricsStayOmitted()
  {
    // Add private values to the canonical public profile; this is client behavior coverage,
    // not a claim that the canonical fixture contains owner-private counts.
    const string key = "rss_feed_items_hidden";
    var profile = JsonSerializer.Deserialize<UserResponse>(
        ApiFixtureLoader.LoadResponse("native.users.profile.default"), VouchaApiJson.Options)!;
    var metrics = new UserMetrics("user_metrics", profile.User.Id, new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int>
        {
          [key] = 1000,
          ["rss_feed_items_viewed"] = 9,
          ["rss_feed_items_saved"] = 3,
          ["rss_feeds_subscribed"] = 3,
          ["rss_feeds_muted"] = 3,
          ["rss_feeds_viewed"] = 3,
          ["topics_subscribed_posts"] = 3,
          ["topics_subscribed_news"] = 3,
        },
        PrivateCountCapped: new Dictionary<string, bool> { [key] = true });
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var model = NewViewModel(profile.User, metrics: metrics,
        localization: new UiLocalization(controller), localeController: controller);
    model.SetCurrentViewer(profile.User.Id, profile.User.Username);
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(model.PrivateMetricRows);
    Assert.Equal(key, row.Key);
    Assert.Equal("Hidden RSS Items", row.Label);
    Assert.Equal("1,000+", row.CountLabel);
    controller.ApplySavedLocale("es");
    Assert.Equal("1.000+", Assert.Single(model.PrivateMetricRows).CountLabel);

    model.SetCurrentViewer(null, null);
    Assert.Empty(model.PrivateMetricRows);
    model.SetCurrentViewer(profile.User.Id, profile.User.Username);
    Assert.Empty(model.PrivateMetricRows);
  }
}
