using System.Net;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public void CommunityBrowseCardRendersTrustedAppAndHidesAbsentProvenance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var client = Client("{}");
    var page = new CommunityBrowsePage(new CommunityBrowseViewModel(new ApiCommunitiesService(client)), new AnonymousSessionStore());
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page));
    var card = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    var label = Assert.Single(VisualTreeElements.Descendants<Label>(card), label => label.AutomationId == "CommunityPublicProvenance");
    var trusted = new CommunityBrowseRow("community-1", "Community", "community", 0, 0, false,
        new PublicContentProvenance("mcp", new PublicProvenanceApp("verified", ClientName: "Fixture Agent")));

    card.BindingContext = trusted;
    Assert.True(label.IsVisible);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);

    card.BindingContext = trusted with { Provenance = null };
    Assert.False(label.IsVisible);
    Assert.Null(label.Text);

    card.BindingContext = trusted with
    {
      Provenance = new PublicContentProvenance("mcp", new PublicProvenanceApp("known", Key: "private-catalog-key")),
    };
    Assert.True(label.IsVisible);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.SharedProvenanceViaMcp), label.Text);
    Assert.DoesNotContain("private-catalog-key", label.Text, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CommunityDetailHeaderRendersTrustedProvenanceAndRefreshesItsLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    var detail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    detail["community"]!["provenance"] = JsonNode.Parse("""
        {"via":"mcp","app":{"kind":"verified","client_id":"fixture-agent","client_name":"Fixture Agent"}}
        """);
    var client = Client(detail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"));
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(client),
        localization: localization, localeController: controller);
    var page = new CommunityDetailPage(model, new AnonymousSessionStore());
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var label = Assert.Single(VisualTreeElements.Descendants<Label>(page), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    var english = label.Text;

    controller.ApplySavedLocale("es");

    Assert.True(label.IsVisible);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);
    Assert.NotEqual(english, label.Text);
  }

  [Fact]
  public async Task RoutedCommunityOverviewHeaderRendersAndLocalizesProvenance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    var detail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    detail["community"]!["provenance"] = JsonNode.Parse("""
        {"via":"mcp","app":{"kind":"verified","client_id":"fixture-agent","client_name":"Fixture Agent"}}
        """);
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        detail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"))),
        localization: localization, localeController: controller);
    var page = new CommunityOverviewPage(model, new AnonymousSessionStore());

    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);

    var label = Assert.Single(VisualTreeElements.Descendants<Label>(page), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    var english = label.Text;
    Assert.True(label.IsVisible);

    controller.ApplySavedLocale("es");

    Assert.NotEqual(english, label.Text);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ListsPageCardAndSelectedHeaderRenderProvenanceAndRefreshLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    using var model = new ListsViewModel(Client(Fixture("entity-provenance.lists"),
        Fixture("native.list-items.default")), localization, controller);
    var page = new ListsPage(model, new AnonymousSessionStore());
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var list = Assert.Single(model.Lists, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<ListSummaryRow>);
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    card.BindingContext = list;
    var cardLabel = Assert.Single(VisualTreeElements.Descendants<Label>(card), label =>
        label.Text == list.LocalizedProvenanceLabel);
    Assert.True(cardLabel.IsVisible);
    Assert.NotNull(model.SelectedList);
    var headerLabel = Assert.Single(VisualTreeElements.Descendants<Label>(page), label =>
        label.Text == model.SelectedList.LocalizedProvenanceLabel);
    Assert.True(headerLabel.IsVisible);
    var english = headerLabel.Text;

    controller.ApplySavedLocale("es");

    Assert.NotEqual(english, headerLabel.Text);
    Assert.Equal(model.SelectedList.LocalizedProvenanceLabel, headerLabel.Text);
    Assert.Contains("Fixture Agent", headerLabel.Text, StringComparison.Ordinal);
  }

  [Fact]
  public async Task TopicsPageCardRendersOnlyPublicProvenance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var client = Client(Fixture("entity-provenance.topics"));
    using var model = new TopicsViewModel(new ApiTopicsService(client));
    var page = new TopicsPage(model, new AnonymousSessionStore(), null!, null!, null!, client,
        Recovery(locale), null!, UiLocalization.English, null!, new AppConfig(new Uri("https://api.test")));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<TopicPageRow>);
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    card.BindingContext = Assert.Single(Assert.IsType<TopicsPageBinding>(page.BindingContext).Items,
        row => row.Topic.Provenance?.App?.ClientName == "Fixture Agent");
    var label = Assert.Single(VisualTreeElements.Descendants<Label>(card), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);

    card.BindingContext = Assert.Single(Assert.IsType<TopicsPageBinding>(page.BindingContext).Items,
        row => !row.HasProvenance);
    Assert.False(label.IsVisible);
    Assert.Null(label.Text);
  }

  [Fact]
  public async Task TopicDetailHeaderRendersTrustedProvenanceAndRefreshesLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    var fixture = JsonNode.Parse(Fixture("entity-provenance.topics"))!;
    var topic = fixture["topics"]!["topic-1-mcp"]!.DeepClone();
    var client = Client(new JsonObject { ["topic"] = topic }.ToJsonString());
    using var model = new TopicDetailViewModel(new ApiTopicsService(client),
        localization: localization, localeController: controller);
    var page = new TopicDetailPage(model, new AnonymousSessionStore(), "topic-1-mcp",
        Recovery(controller), new AppConfig(new Uri("https://api.test")));
    await model.LoadAsync("topic-1-mcp", followSource: false, TestContext.Current.CancellationToken);
    var label = Assert.Single(VisualTreeElements.Descendants<Label>(page), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    var english = label.Text;

    controller.ApplySavedLocale("es");

    Assert.NotEqual(english, label.Text);
    Assert.Equal(model.LocalizedProvenanceLabel, label.Text);
  }

  [Fact]
  public async Task SourceDetailHeaderRendersRssFeedProvenanceInsteadOfItsTopicProvenance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var topic = JsonNode.Parse(Fixture("entity-provenance.topics"))!["topics"]!["topic-1-mcp"]!.DeepClone();
    topic["topic_type"] = "rss_feed";
    topic["provenance"] = JsonNode.Parse("""
        {"via":"api","app":{"kind":"verified","client_id":"topic-agent","client_name":"Topic Agent"}}
        """);
    var topicClient = Client(
        new JsonObject { ["topic"] = topic.DeepClone() }.ToJsonString(),
        Fixture("entity-provenance.rss-feeds"));
    using var topicModel = new TopicDetailViewModel(new ApiTopicsService(topicClient));
    var topicPage = new TopicDetailPage(topicModel, new AnonymousSessionStore(), "topic-1-mcp", Recovery(locale),
        new AppConfig(new Uri("https://api.test")));
    var topicLoaded = WaitUntilLoadedAsync(topicModel);
    Appear(topicPage);
    await topicLoaded;
    var topicLabel = Assert.Single(VisualTreeElements.Descendants<Label>(topicPage), item =>
        item.Text?.Contains("Topic Agent", StringComparison.Ordinal) == true);
    Assert.True(topicLabel.IsVisible);

    var sourceClient = Client(
        new JsonObject { ["topic"] = topic }.ToJsonString(),
        Fixture("entity-provenance.rss-feeds"));
    using var sourceModel = new TopicDetailViewModel(new ApiTopicsService(sourceClient));
    var sourcePage = new TopicDetailPage(sourceModel, new AnonymousSessionStore(), "topic-1-mcp", Recovery(locale),
        new AppConfig(new Uri("https://api.test")), isSourceDetail: true);
    var sourceLoaded = WaitUntilLoadedAsync(sourceModel);
    Appear(sourcePage);
    await sourceLoaded;

    var label = Assert.Single(VisualTreeElements.Descendants<Label>(sourcePage), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    Assert.DoesNotContain("Topic Agent", label.Text, StringComparison.Ordinal);
  }

  [Fact]
  public async Task FullRssSourceCardRendersProvenanceButAbsentSourceDoesNot()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(locale);
    var client = Client(Fixture("entity-provenance.rss-feeds"));
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(client),
        NewsFeedKind.News, NewsFeedScope.AllSources, localization: localization, localeController: locale);
    var page = new NewsFeedsPage(model, new AnonymousSessionStore(), null!,
        Recovery(locale));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<NewsFeedItem>);
    var originalItems = model.Items;
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    var verified = Assert.Single(model.Items, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    card.BindingContext = verified;
    var label = Assert.Single(VisualTreeElements.Descendants<Label>(card), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    var englishLabel = label.Text;

    card.BindingContext = Assert.Single(model.Items, row => !row.HasProvenance);
    Assert.False(label.IsVisible);
    Assert.Null(label.Text);

    card.BindingContext = verified;
    Assert.True(label.IsVisible);
    Assert.Equal(englishLabel, label.Text);
    locale.ApplySavedLocale("es");

    var refreshedItems = Assert.IsAssignableFrom<IReadOnlyList<NewsFeedItem>>(collection.ItemsSource);
    Assert.NotSame(originalItems, refreshedItems);
    Assert.Equal(model.Items, refreshedItems);
    var refreshed = Assert.Single(refreshedItems, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    Assert.NotSame(verified, refreshed);
    card.BindingContext = refreshed;
    Assert.True(label.IsVisible);
    Assert.NotEqual(englishLabel, label.Text);
    Assert.Equal(refreshed.LocalizedProvenanceLabel, label.Text);
  }

  [Fact]
  public async Task StaffChannelCardsClearWhenViewerLosesStaffRole()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var facts = new StaffContentProvenance("mcp",
        new StaffProvenanceOAuthClient("internal-client-42", "Fixture Agent", null, true));

    var communities = new CommunityBrowsePage(
        new CommunityBrowseViewModel(new ApiCommunitiesService(Client("{}"))), new AnonymousSessionStore());
    var communityCollection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(communities));
    var communityCard = Assert.IsType<VerticalStackLayout>(communityCollection.ItemTemplate.CreateContent());
    var communityLabel = Assert.Single(VisualTreeElements.Descendants<Label>(communityCard), label =>
        label.AutomationId == "CommunityStaffChannel");
    var community = new CommunityBrowseRow("id", "Community", "community", 0, 0, false,
        StaffProvenance: facts, IsStaffViewer: true);
    communityCard.BindingContext = community;
    Assert.True(communityLabel.IsVisible);
    communityCard.BindingContext = community with { IsStaffViewer = false };
    Assert.False(communityLabel.IsVisible);
    Assert.Null(communityLabel.Text);

    using var listsModel = new ListsViewModel(Client(Fixture("entity-provenance.lists"),
        Fixture("native.list-items.default")));
    var lists = new ListsPage(listsModel, new AnonymousSessionStore());
    await listsModel.LoadAsync(TestContext.Current.CancellationToken);
    var listCollection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(lists), view =>
        view.ItemsSource is IReadOnlyList<ListSummaryRow>);
    var listCard = Assert.IsType<Border>(listCollection.ItemTemplate.CreateContent());
    var list = listsModel.Lists[0] with { StaffProvenance = facts, IsStaffViewer = true };
    listCard.BindingContext = list;
    var listLabel = Assert.Single(VisualTreeElements.Descendants<Label>(listCard), item =>
        item.Text == list.StaffChannelLabel);
    Assert.True(listLabel.IsVisible);
    listCard.BindingContext = list with { IsStaffViewer = false };
    Assert.False(listLabel.IsVisible);
    Assert.Null(listLabel.Text);

    var topicClient = Client(Fixture("entity-provenance.topics"));
    using var topicsModel = new TopicsViewModel(new ApiTopicsService(topicClient));
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var topics = new TopicsPage(topicsModel, new AnonymousSessionStore(), null!, null!, null!, topicClient,
        Recovery(locale), null!, UiLocalization.English, null!, new AppConfig(new Uri("https://api.test")));
    await topicsModel.LoadAsync(TestContext.Current.CancellationToken);
    var topicCollection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(topics), view =>
        view.ItemsSource is IReadOnlyList<TopicPageRow>);
    var topicCard = Assert.IsType<Border>(topicCollection.ItemTemplate.CreateContent());
    var topic = TopicPageRow.Create(topicsModel.Items[0] with { StaffProvenance = facts, IsStaffViewer = true },
        new AppConfig(new Uri("https://api.test")));
    topicCard.BindingContext = topic;
    var topicLabel = Assert.Single(VisualTreeElements.Descendants<Label>(topicCard), item =>
        item.Text == topic.StaffChannelLabel);
    Assert.True(topicLabel.IsVisible);
    topicCard.BindingContext = TopicPageRow.Create(topic.Topic with { IsStaffViewer = false },
        new AppConfig(new Uri("https://api.test")));
    Assert.False(topicLabel.IsVisible);
    Assert.Null(topicLabel.Text);

    using var feedsModel = new NewsFeedsViewModel(new ApiNewsFeedService(Client(Fixture("entity-provenance.rss-feeds"))),
        NewsFeedKind.News, NewsFeedScope.AllSources);
    var feeds = new NewsFeedsPage(feedsModel, new AnonymousSessionStore(), null!, Recovery(locale));
    await feedsModel.LoadAsync(TestContext.Current.CancellationToken);
    var feedCollection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(feeds), view =>
        view.ItemsSource is IReadOnlyList<NewsFeedItem>);
    var feedCard = Assert.IsType<Border>(feedCollection.ItemTemplate.CreateContent());
    var source = feedsModel.Items.First(item => item.IsSource) with
    { StaffProvenance = facts, IsStaffViewer = true };
    feedCard.BindingContext = source;
    var sourceLabel = Assert.Single(VisualTreeElements.Descendants<Label>(feedCard), item =>
        item.Text == source.StaffChannelLabel);
    Assert.True(sourceLabel.IsVisible);
    feedCard.BindingContext = source with { IsStaffViewer = false };
    Assert.False(sourceLabel.IsVisible);
    Assert.Null(sourceLabel.Text);
  }

  private static string Fixture(string id)
  {
    var manifest = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")))!;
    var path = manifest["fixtures"]!.AsArray().Single(entry => (string?)entry?["id"] == id)!["bodyFile"]!.GetValue<string>();
    return File.ReadAllText(FilamentsContractPaths.ApiFixture(path));
  }

  private static void InstallPageStyles()
  {
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["VoteClearEligibilityConverter"] = new VoteClearEligibilityConverter();
    app.Resources["StoryDiscussionVisibilityConverter"] = new StoryDiscussionVisibilityConverter();
    app.Resources["FollowerDistributionVisibilityConverter"] = new FollowerDistributionVisibilityConverter();
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new StubDeviceLanguageProvider("en")));
  }

  private static VouchaApiClient Client(params string[] bodies) =>
      new(new HttpClient(new ResponseHandler(bodies)) { BaseAddress = new Uri("https://api.test") });

  private static EmailVerificationRecoveryCoordinator Recovery(IUiLocaleController locale) =>
      new(DispatchProxy.Create<IEmailAddressService, UnusedService>(), UiLocalization.English, locale);

  public class UnusedService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        throw new NotSupportedException(method?.Name);
  }

  private static void Appear(TopicDetailPage page) =>
      typeof(TopicDetailPage).GetMethod("OnAppearing",
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
          null, Type.EmptyTypes, null)!.Invoke(page, null);

  private static Task WaitUntilLoadedAsync(TopicDetailViewModel viewModel)
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnStateChanged(object? sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName != nameof(viewModel.State)) return;
      if (viewModel.State == LoadState.Loaded) completion.TrySetResult();
      else if (viewModel.State == LoadState.Error)
        completion.TrySetException(new InvalidOperationException(viewModel.ErrorMessage ?? "Topic detail failed to load."));
    }

    viewModel.PropertyChanged += OnStateChanged;
    if (viewModel.State == LoadState.Loaded) completion.TrySetResult();
    return WaitAndUnsubscribeAsync();

    async Task WaitAndUnsubscribeAsync()
    {
      try
      {
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      }
      finally
      {
        viewModel.PropertyChanged -= OnStateChanged;
      }
    }
  }

  private sealed class ResponseHandler(IEnumerable<string> bodies) : HttpMessageHandler
  {
    private readonly Queue<string> responses = new(bodies);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
          RequestMessage = request,
        });
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }
  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new Timer();
  }
  private sealed class Timer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
