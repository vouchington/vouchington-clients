using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task TopicDetailReappearingRecordsAnotherFeedVisit()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var topic = JsonNode.Parse(Fixture("entity-provenance.topics"))!["topics"]!["topic-1-mcp"]!.DeepClone();
    topic["topic_type"] = "rss_feed";
    var response = new JsonObject { ["topic"] = topic }.ToJsonString();
    var feeds = Fixture("entity-provenance.rss-feeds.staff");
    var signals = new LifecycleSignals();
    using var model = new TopicDetailViewModel(new ApiTopicsService(Client(response, feeds, response, feeds)),
        productSignals: signals);
    var page = new TopicDetailPage(model, new MutableStaffSessionStore(), "topic-1-mcp",
        Recovery(locale), new AppConfig(new Uri("https://api.test")));

    var firstLoad = WaitUntilLoadedAsync(model);
    Appear(page);
    await firstLoad;
    Assert.Single(signals.FeedViews);
    typeof(TopicDetailPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
    Appear(page);
    await signals.SecondVisit.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    Assert.Equal(2, signals.FeedViews.Count);
    Assert.Equal(signals.FeedViews[0], signals.FeedViews[1]);
  }

  [Fact]
  public async Task OrdinaryAccountSwitchReloadsTheVisibleTopicWithoutStaffAccessGain()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var topic = JsonNode.Parse(Fixture("entity-provenance.topics"))!["topics"]!["topic-1-mcp"]!.DeepClone();
    topic["topic_type"] = "rss_feed";
    var response = new JsonObject { ["topic"] = topic }.ToJsonString();
    var feeds = Fixture("entity-provenance.rss-feeds");
    var signals = new LifecycleSignals();
    using var model = new TopicDetailViewModel(new ApiTopicsService(Client(response, feeds, response, feeds)),
        productSignals: signals);
    var session = new MutableStaffSessionStore();
    session.SetCurrentWithoutNotification(new SessionSnapshot(new User("first", "first")));
    var page = new TopicDetailPage(model, session, "topic-1-mcp", Recovery(locale), new AppConfig(new Uri("https://api.test")));
    var initialLoad = WaitUntilLoadedAsync(model);
    Appear(page);
    await initialLoad;

    session.SetCurrentWithoutNotification(new SessionSnapshot(new User("second", "second")));
    await page.RefreshAfterSessionChangedAsync();

    Assert.Equal(LoadState.Loaded, model.State);
    Assert.Equal(2, signals.FeedViews.Count);
    await page.RefreshAfterSessionChangedAsync();
    Assert.Equal(2, signals.FeedViews.Count);
  }

  private sealed class LifecycleSignals : IProductSurfaceAnalytics
  {
    public List<string> FeedViews { get; } = [];
    public TaskCompletionSource SecondVisit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void RecordRssFeedView(string rssFeedId)
    {
      FeedViews.Add(rssFeedId);
      if (FeedViews.Count == 2) SecondVisit.TrySetResult();
    }
    public void RecordRssFeedItemView(string itemId) => throw new NotSupportedException();
    public void RecordLandingPageVisit(string landingPageId) => throw new NotSupportedException();
    public void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal) => throw new NotSupportedException();
  }
}
