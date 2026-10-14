using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task MountedTopicDetailFetchesStaffFactsAfterViewerTierGain()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new DeviceLanguage("en"));
    var publicTopic = JsonNode.Parse(Fixture("entity-provenance.topics"))!["topics"]!["topic-1-mcp"]!.DeepClone();
    var staffTopic = publicTopic.DeepClone();
    staffTopic["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":null}
        """);
    using var model = new TopicDetailViewModel(new ApiTopicsService(Client(
        new JsonObject { ["topic"] = publicTopic }.ToJsonString(),
        new JsonObject { ["topic"] = staffTopic }.ToJsonString())));
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var page = new TopicDetailPage(
        model,
        session,
        "topic-1-mcp",
        Recovery(locale),
        new AppConfig(new Uri("https://api.test")));
    var initialLoad = WaitUntilLoadedAsync(model);
    Appear(page);
    await initialLoad;
    Assert.Null(model.StaffChannelLabel);

    session.SetCurrentWithoutNotification(new SessionSnapshot(new User("staff", "staff", Roles: ["moderator"])));
    await page.RefreshAfterSessionChangedAsync();
    Assert.Equal("Channel: mcp", model.StaffChannelLabel);
  }

  [Fact]
  public async Task EditableCommunityDetailReappearingAfterStaffSignOutClearsCachedFacts()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var detail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    detail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":{"client_id":"internal-client-42","client_name":"Fixture Agent","metadata_url":null,"verified":true}}
        """);
    var posts = JsonNode.Parse(Fixture("web.communities.posts.default"))!;
    posts["posts"]!["post-1"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":{"client_id":"internal-client-42","client_name":"Fixture Agent","metadata_url":null,"verified":true}}
        """);
    var session = new MutableStaffSessionStore();
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        detail.ToJsonString(), Fixture("web.communities.members.default"), posts.ToJsonString(),
        Fixture("web.communities.list-items.counts.default"))), session);
    var page = new CommunityDetailPage(model, session);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    Assert.Null(page.Slug);
    InvokeLifecycle(page, "OnAppearing");
    Assert.Contains(Descendants<Label>(page), label =>
        label.IsVisible && label.Text?.Contains("internal-client-42", StringComparison.Ordinal) == true);
    Assert.Contains(Descendants<Label>(page), label => label.Text == "Channel: mcp");

    InvokeLifecycle(page, "OnDisappearing");
    session.SetCurrent(SessionSnapshot.Anonymous);
    Assert.Contains(Descendants<Label>(page), label => label.Text == "Channel: mcp");
    InvokeLifecycle(page, "OnAppearing");
    Assert.DoesNotContain(Descendants<Label>(page), label =>
        label.IsVisible && label.Text?.Contains("internal-client-42", StringComparison.Ordinal) == true);
    Assert.DoesNotContain(Descendants<Label>(page), label => label.Text == "Channel: mcp");
  }

  [Fact]
  public async Task RoutedCommunitySectionReprojectsStaffFactsOnActiveAndHiddenSessionChanges()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var detail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    detail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":{"client_id":"internal-client-42","client_name":"Fixture Agent","metadata_url":null,"verified":true}}
        """);
    var posts = JsonNode.Parse(Fixture("web.communities.posts.default"))!;
    posts["posts"]!["post-1"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":{"client_id":"internal-client-42","client_name":"Fixture Agent","metadata_url":null,"verified":true}}
        """);
    var session = new MutableStaffSessionStore();
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        detail.ToJsonString(), Fixture("web.communities.members.default"), posts.ToJsonString(),
        Fixture("web.communities.list-items.counts.default"))), session);
    var overview = new CommunityOverviewPage(model, session);
    var postsPage = new CommunityPostsPage(model, session);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    InvokeLifecycle(overview, "OnAppearing");
    InvokeLifecycle(postsPage, "OnAppearing");

    var channel = Assert.Single(Descendants<Label>(overview), label =>
        label.AutomationId == "CommunitySectionStaffChannel");
    var client = Assert.Single(Descendants<Label>(overview), label =>
        label.AutomationId == "CommunitySectionStaffClient");
    var verified = Assert.Single(Descendants<Label>(overview), label =>
        label.AutomationId == "CommunitySectionStaffVerification");
    Assert.True(channel.IsVisible);
    Assert.True(client.IsVisible);
    Assert.True(verified.IsVisible);
    Assert.Contains("Fixture Agent", client.Text, StringComparison.Ordinal);
    Assert.Contains("internal-client-42", client.Text, StringComparison.Ordinal);

    var rows = Assert.Single(Descendants<CollectionView>(postsPage), view =>
        view.ItemsSource is IReadOnlyList<CommunitySummaryRow>);
    var staffPost = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CommunitySummaryRow>>(
        rows.ItemsSource), row => row.Id == "post-1");
    Assert.True(staffPost.HasStaffChannel);

    session.SetCurrent(SessionSnapshot.Anonymous);
    Assert.False(channel.IsVisible);
    Assert.False(client.IsVisible);
    Assert.False(verified.IsVisible);
    Assert.All(Assert.IsAssignableFrom<IReadOnlyList<CommunitySummaryRow>>(
        rows.ItemsSource), row => Assert.False(row.HasStaffChannel));

    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    Assert.True(channel.IsVisible);
    InvokeLifecycle(overview, "OnDisappearing");
    InvokeLifecycle(postsPage, "OnDisappearing");
    session.SetCurrent(SessionSnapshot.Anonymous);
    Assert.True(channel.IsVisible);
    InvokeLifecycle(overview, "OnAppearing");
    InvokeLifecycle(postsPage, "OnAppearing");
    Assert.False(channel.IsVisible);
    Assert.False(client.IsVisible);
    Assert.False(verified.IsVisible);
    Assert.All(Assert.IsAssignableFrom<IReadOnlyList<CommunitySummaryRow>>(
        rows.ItemsSource), row => Assert.False(row.HasStaffChannel));
  }

  [Fact]
  public async Task CommunityBrowseReappearingAfterStaffSignOutClearsCachedStaffChannel()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var session = new MutableStaffSessionStore();
    var model = new CommunityBrowseViewModel(new ApiCommunitiesService(Client(
        Fixture("entity-provenance.communities.staff"))));
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.NotEmpty(model.Results);
    var page = new CommunityBrowsePage(model, session);
    var collection = Assert.Single(Descendants<CollectionView>(page));

    InvokeLifecycle(page, "OnAppearing");
    var staffRow = Assert.Single(
        Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(collection.ItemsSource),
        row => row.Id == "community-1-mcp");
    Assert.True(staffRow.HasStaffChannel);

    InvokeLifecycle(page, "OnDisappearing");
    session.SetCurrent(SessionSnapshot.Anonymous);
    Assert.True(Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(
        collection.ItemsSource), row => row.Id == "community-1-mcp").HasStaffChannel);
    InvokeLifecycle(page, "OnAppearing");
    var anonymousRows = Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(collection.ItemsSource);
    var anonymousRow = Assert.Single(anonymousRows, row => row.Id == "community-1-mcp");
    Assert.False(anonymousRow.HasStaffChannel);
    Assert.All(anonymousRows, row => Assert.False(row.HasStaffChannel));
    var card = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    card.BindingContext = anonymousRow;
    var staffLabel = Assert.Single(card.Children.OfType<Label>(), label =>
        label.AutomationId == "CommunityStaffChannel");
    Assert.False(staffLabel.IsVisible);
    Assert.Null(staffLabel.Text);
  }

  [Fact]
  public async Task CommunityBrowseReappearingAsStaffReloadsPublicCachedRows()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var model = new CommunityBrowseViewModel(new ApiCommunitiesService(Client(
        Fixture("entity-provenance.communities"), Fixture("entity-provenance.communities.staff"))));
    await model.SearchAsync(TestContext.Current.CancellationToken);
    var page = new CommunityBrowsePage(model, session);
    var collection = Assert.Single(Descendants<CollectionView>(page));
    InvokeLifecycle(page, "OnAppearing");
    Assert.All(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(
        collection.ItemsSource), row => Assert.False(row.HasStaffChannel));

    InvokeLifecycle(page, "OnDisappearing");
    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnResultsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
      if (args.PropertyName == nameof(CommunityBrowseViewModel.Results)
          && model.Results.Any(row => row.StaffProvenance is not null)) refreshed.TrySetResult();
    }
    model.PropertyChanged += OnResultsChanged;
    try
    {
      InvokeLifecycle(page, "OnAppearing");
      await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
    finally
    {
      model.PropertyChanged -= OnResultsChanged;
    }

    var staffRow = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(
        collection.ItemsSource), row => row.Id == "community-1-mcp");
    Assert.True(staffRow.HasStaffChannel);
    InvokeLifecycle(page, "OnDisappearing");
    InvokeLifecycle(page, "OnAppearing");
    Assert.True(Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(
        collection.ItemsSource), row => row.Id == "community-1-mcp").HasStaffChannel);
  }

  [Fact]
  public async Task ListsReattachPublishesCachedRowsUnderCurrentViewer()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var model = new ListsViewModel(Client(Fixture("entity-provenance.lists.staff"),
        Fixture("native.list-items.default")));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var session = new MutableStaffSessionStore();
    var page = new ListsPage(model, session);
    var binding = Assert.IsType<ListsPageBinding>(page.BindingContext);
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<ListSummaryRow>);

    binding.AttachSessionChanged();
    Assert.True(Assert.Single(binding.Lists, row => row.Id == "list-1-mcp").HasStaffChannel);
    binding.DetachSessionChanged();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var published = false;
    binding.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ListsPageBinding.Lists)) published = true;
    };
    binding.AttachSessionChanged();
    Assert.True(published);
    Assert.All(binding.Lists, row => Assert.False(row.HasStaffChannel));
    Assert.All(Assert.IsAssignableFrom<IReadOnlyList<ListSummaryRow>>(
        collection.ItemsSource), row => Assert.False(row.HasStaffChannel));
  }

  [Fact]
  public async Task MountedListsRefetchWhenViewerGainsStaffAccess()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var publicLists = JsonNode.Parse(Fixture("entity-provenance.lists.staff"))!;
    foreach (var list in publicLists["lists"]!.AsObject().Select(entry => entry.Value))
      list!.AsObject().Remove("staff_provenance");
    using var model = new ListsViewModel(Client(publicLists.ToJsonString(),
        Fixture("native.list-items.default"), Fixture("entity-provenance.lists.staff"),
        Fixture("native.list-items.default")));
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var binding = new ListsPageBinding(model, session);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(Assert.Single(binding.Lists, row => row.Id == "list-1-mcp").HasStaffChannel);

    binding.AttachSessionChanged();
    session.SetCurrentWithoutNotification(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    await binding.RefreshAfterSessionChangedAsync();
    Assert.True(Assert.Single(binding.Lists, row => row.Id == "list-1-mcp").HasStaffChannel);
    binding.DetachSessionChanged();
  }

  [Fact]
  public async Task ListsReappearanceAfterDetachedDemotionAllowsMountedStaffRefetch()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var staffLists = Fixture("entity-provenance.lists.staff");
    var publicLists = JsonNode.Parse(staffLists)!;
    foreach (var list in publicLists["lists"]!.AsObject().Select(entry => entry.Value))
      list!.AsObject().Remove("staff_provenance");
    using var model = new ListsViewModel(Client(
        staffLists, Fixture("native.list-items.default"),
        publicLists.ToJsonString(), Fixture("native.list-items.default"),
        staffLists, Fixture("native.list-items.default")));
    var session = new MutableStaffSessionStore();
    var binding = new ListsPageBinding(model, session);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    binding.AttachSessionChanged();
    binding.DetachSessionChanged();
    session.SetCurrent(SessionSnapshot.Anonymous);
    binding.AttachSessionChanged();
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(Assert.Single(binding.Lists, row => row.Id == "list-1-mcp").HasStaffChannel);

    session.SetCurrentWithoutNotification(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    await binding.RefreshAfterSessionChangedAsync();
    Assert.True(Assert.Single(binding.Lists, row => row.Id == "list-1-mcp").HasStaffChannel);
    binding.DetachSessionChanged();
  }


  [Fact]
  public async Task FeedAndTopicReattachPublishCachedStaffRowsUnderCurrentViewer()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var session = new MutableStaffSessionStore();
    using var feedsModel = new NewsFeedsViewModel(new ApiNewsFeedService(Client(
        Fixture("entity-provenance.rss-feeds.staff"))), NewsFeedKind.News, NewsFeedScope.AllSources);
    await feedsModel.LoadAsync(TestContext.Current.CancellationToken);
    var feeds = new NewsFeedsPageBinding(feedsModel, session);
    using var topicsModel = new TopicsViewModel(new ApiTopicsService(Client(
        Fixture("entity-provenance.topics.staff"))));
    await topicsModel.LoadAsync(TestContext.Current.CancellationToken);
    var topics = new TopicsPageBinding(topicsModel, session, new AppConfig(new Uri("https://api.test")));
    Assert.Contains(feeds.Items, row => row.HasStaffChannel);
    Assert.Contains(topics.Items, row => row.HasStaffChannel);

    feeds.AttachSessionChanged();
    topics.AttachSessionChanged();
    feeds.DetachSessionChanged();
    topics.DetachSessionChanged();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var feedPublished = false;
    var topicPublished = false;
    feeds.PropertyChanged += (_, args) => feedPublished |= args.PropertyName == nameof(NewsFeedsPageBinding.Items);
    topics.PropertyChanged += (_, args) => topicPublished |= args.PropertyName == nameof(TopicsPageBinding.Items);
    feeds.AttachSessionChanged();
    topics.AttachSessionChanged();

    Assert.True(feedPublished);
    Assert.True(topicPublished);
    Assert.All(feeds.Items, row => Assert.False(row.HasStaffChannel));
    Assert.All(topics.Items, row => Assert.False(row.HasStaffChannel));
  }

  private static void InvokeLifecycle(CommunityBrowsePage page, string method) =>
      typeof(CommunityBrowsePage).GetMethod(method,
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);

  private static void InvokeLifecycle(CommunityDetailPage page, string method) =>
      typeof(CommunityDetailPage).GetMethod(method,
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);

  private static void InvokeLifecycle(CommunitySectionPage page, string method) =>
      typeof(CommunitySectionPage).GetMethod(method,
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);

  private sealed class MutableStaffSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("staff-1", "staff", Roles: ["moderator"]));
    public void SetCurrent(SessionSnapshot next)
    {
      Current = next;
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(next));
    }
    public void SetCurrentWithoutNotification(SessionSnapshot next) => Current = next;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
