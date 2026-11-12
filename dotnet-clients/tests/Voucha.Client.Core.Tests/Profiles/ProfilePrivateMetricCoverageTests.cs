using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  private static readonly Dictionary<string, string> NewMetricLabels = new()
  {
    ["rss_feed_items_saved"] = "Saved RSS Items",
    ["rss_feed_items_viewed"] = "Recently Viewed RSS Items",
    ["rss_feeds_subscribed"] = "Subscribed RSS Feeds",
    ["rss_feeds_muted"] = "Muted RSS Feeds",
    ["rss_feeds_viewed"] = "Recently Viewed RSS Feeds",
    ["topics_subscribed_posts"] = "Topics Subscribed for Posts",
    ["topics_subscribed_news"] = "Topics Subscribed for News",
    ["rss_feed_items_hidden"] = "Hidden RSS Items",
  };

  [Fact]
  public async Task OwnerFixtureRendersEveryPrivateMetricAndCappedOnesWithPlus()
  {
    var profile = LoadProfile("native.users.profile.private-counts.owner");
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var model = NewViewModel(profile.User, metrics: profile.UserMetrics,
        localization: new UiLocalization(controller), localeController: controller);
    model.SetCurrentViewer(profile.User.Id, profile.User.Username);
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);

    var rows = model.PrivateMetricRows.ToDictionary(row => row.Key);
    Assert.Equal(26, rows.Count);
    Assert.Equal(profile.UserMetrics!.PrivateCount!.Keys.Order(), rows.Keys.Order());
    var capped = new HashSet<string> { "rss_feed_items_hidden", "rss_feed_items_saved", "topics_viewed" };
    foreach (var (key, row) in rows)
    {
      Assert.Equal(capped.Contains(key), row.IsCapped);
      Assert.Equal(capped.Contains(key) ? "1,000+" : profile.UserMetrics.PrivateCount[key].ToString(), row.CountLabel);
    }
    foreach (var (key, label) in NewMetricLabels) Assert.Equal(label, rows[key].Label);
    Assert.Equal(26, rows.Values.Select(row => row.Label).Distinct().Count());
    controller.ApplySavedLocale("es");
    Assert.Equal("1.000+", model.PrivateMetricRows.Single(row => row.Key == "rss_feed_items_saved").CountLabel);
  }

  [Theory]
  [InlineData("native.users.profile.default")]
  [InlineData("native.users.profile.restricted")]
  public async Task NonOwnerFixturesShowNoPrivateRows(string fixture)
  {
    var profile = LoadProfile(fixture);
    Assert.Null(profile.UserMetrics?.PrivateCount);
    using var model = NewViewModel(profile.User, metrics: profile.UserMetrics);
    model.SetCurrentViewer(profile.User.Id, profile.User.Username);
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);
    Assert.Empty(model.PrivateMetricRows);
    Assert.False(model.HasPrivateMetrics);
  }

  [Fact]
  public async Task MissingNewMetricsProduceNoRowsAndNoInventedZeros()
  {
    var profile = LoadProfile("native.users.profile.private-counts.owner");
    var counts = profile.UserMetrics!.PrivateCount!
        .Where(entry => !NewMetricLabels.ContainsKey(entry.Key)).ToDictionary(e => e.Key, e => e.Value);
    var metrics = profile.UserMetrics with { PrivateCount = counts, PrivateCountCapped = null };
    using var model = NewViewModel(profile.User, metrics: metrics);
    model.SetCurrentViewer(profile.User.Id, profile.User.Username);
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.Equal(counts.Count, model.PrivateMetricRows.Count);
    Assert.DoesNotContain(model.PrivateMetricRows, row => NewMetricLabels.ContainsKey(row.Key));
  }

  private static UserResponse LoadProfile(string fixture) =>
      JsonSerializer.Deserialize<UserResponse>(ApiFixtureLoader.LoadResponse(fixture), VouchaApiJson.Options)!;
}
