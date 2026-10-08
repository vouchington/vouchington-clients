using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public void CommunityBrowseCardRendersTrustedAppAndHidesAbsentProvenance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var client = Client("{}");
    var page = new CommunityBrowsePage(new CommunityBrowseViewModel(new ApiCommunitiesService(client)));
    var collection = Assert.Single(Descendants<CollectionView>(page));
    var card = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    var label = Assert.Single(card.Children.OfType<Label>(), item => item.FontSize == 12);
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
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
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
    var page = new CommunityDetailPage(model);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var label = Assert.Single(Descendants<Label>(page), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    var english = label.Text;

    controller.ApplySavedLocale("es");

    Assert.True(label.IsVisible);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);
    Assert.NotEqual(english, label.Text);
  }

  [Fact]
  public async Task ListsPageCardAndSelectedHeaderRenderProvenanceAndRefreshLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    using var model = new ListsViewModel(Client(Fixture("entity-provenance.lists"),
        Fixture("native.list-items.default")), localization, controller);
    var page = new ListsPage(model);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var list = Assert.Single(model.Lists, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<ListSummaryRow>);
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    card.BindingContext = list;
    var cardLabel = Assert.Single(Descendants<Label>(card), label =>
        label.Text == list.LocalizedProvenanceLabel);
    Assert.True(cardLabel.IsVisible);
    Assert.NotNull(model.SelectedList);
    var headerLabel = Assert.Single(Descendants<Label>(page), label =>
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
    using var locale = new UiLocaleController(new DeviceLanguage("en"));
    var client = Client(Fixture("entity-provenance.topics"));
    using var model = new TopicsViewModel(new ApiTopicsService(client));
    var page = new TopicsPage(model, new AnonymousSessionStore(), null!, null!, null!, client,
        Recovery(locale), null!, UiLocalization.English, null!);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<TopicRow>);
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    card.BindingContext = Assert.Single(model.Items, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    var label = Assert.Single(Descendants<Label>(card), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    Assert.Contains("Fixture Agent", label.Text, StringComparison.Ordinal);

    card.BindingContext = Assert.Single(model.Items, row => !row.HasProvenance);
    Assert.False(label.IsVisible);
    Assert.Null(label.Text);
  }

  [Fact]
  public async Task TopicDetailHeaderRendersTrustedProvenanceAndRefreshesLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var controller = new UiLocaleController(new DeviceLanguage("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    var fixture = JsonNode.Parse(Fixture("entity-provenance.topics"))!;
    var topic = fixture["topics"]!["topic-1-mcp"]!.DeepClone();
    var client = Client(new JsonObject { ["topic"] = topic }.ToJsonString());
    using var model = new TopicDetailViewModel(new ApiTopicsService(client),
        localization: localization, localeController: controller);
    var page = new TopicDetailPage(model, new AnonymousSessionStore(), "topic-1-mcp",
        Recovery(controller));
    await model.LoadAsync("topic-1-mcp", followSource: false, TestContext.Current.CancellationToken);
    var label = Assert.Single(Descendants<Label>(page), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);
    var english = label.Text;

    controller.ApplySavedLocale("es");

    Assert.NotEqual(english, label.Text);
    Assert.Equal(model.LocalizedProvenanceLabel, label.Text);
  }

  [Fact]
  public async Task FullRssSourceCardRendersProvenanceButAbsentSourceDoesNot()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new DeviceLanguage("en"));
    var client = Client(Fixture("entity-provenance.rss-feeds"));
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(client),
        NewsFeedKind.News, NewsFeedScope.AllSources);
    var page = new NewsFeedsPage(model, new AnonymousSessionStore(), null!,
        Recovery(locale));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<NewsFeedItem>);
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    var verified = Assert.Single(model.Items, row => row.Provenance?.App?.ClientName == "Fixture Agent");
    card.BindingContext = verified;
    var label = Assert.Single(Descendants<Label>(card), item =>
        item.Text?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.True(label.IsVisible);

    card.BindingContext = Assert.Single(model.Items, row => !row.HasProvenance);
    Assert.False(label.IsVisible);
    Assert.Null(label.Text);
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
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new DeviceLanguage("en")));
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

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
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

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
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
