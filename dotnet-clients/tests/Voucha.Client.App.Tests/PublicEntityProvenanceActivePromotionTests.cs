using System.ComponentModel;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task MountedCommunityBrowseRefetchesRowsAfterStaffPromotion()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    var model = new CommunityBrowseViewModel(new ApiCommunitiesService(Client(
        Fixture("entity-provenance.communities"), Fixture("entity-provenance.communities.staff"))));
    var page = new CommunityBrowsePage(model, session);
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page));
    var publicRows = WaitForResultsAsync(model, hasStaffFacts: false);
    InvokeLifecycle(page, "OnAppearing");
    await publicRows;
    Assert.All(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(collection.ItemsSource),
        row => Assert.False(row.HasStaffChannel));

    var staffRows = WaitForResultsAsync(model, hasStaffFacts: true);
    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    await staffRows;
    Assert.True(Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CommunityBrowseRow>>(
        collection.ItemsSource), row => row.Id == "community-1-mcp").HasStaffChannel);
    InvokeLifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task MountedCommunityDetailRefetchesEditableSlugAfterStaffPromotion()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var publicDetail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    publicDetail["community"]!.AsObject().Remove("staff_provenance");
    var staffDetail = publicDetail.DeepClone();
    staffDetail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":null}
        """);
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        publicDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"),
        staffDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"))), session);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var page = new CommunityDetailPage(model, session);
    Assert.Null(page.Slug);
    Assert.Single(VisualTreeElements.Descendants<Entry>(page)).Text = "test-community";
    InvokeLifecycle(page, "OnAppearing");
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page), label => label.Text == "Channel: mcp");

    var staffFacts = WaitForCommunityStaffFactsAsync(model);
    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    await staffFacts;
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.IsVisible && label.Text == "Channel: mcp");
    InvokeLifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task EditableCommunityDetailRefetchesAfterDetachedStaffPromotion()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var publicDetail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    publicDetail["community"]!.AsObject().Remove("staff_provenance");
    var staffDetail = publicDetail.DeepClone();
    staffDetail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":null}
        """);
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        publicDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"),
        staffDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"))), session);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var page = new CommunityDetailPage(model, session);
    Assert.Null(page.Slug);
    Assert.Single(VisualTreeElements.Descendants<Entry>(page)).Text = "test-community";
    InvokeLifecycle(page, "OnAppearing");
    InvokeLifecycle(page, "OnDisappearing");
    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page), label => label.Text == "Channel: mcp");

    var staffFacts = WaitForCommunityStaffFactsAsync(model);
    InvokeLifecycle(page, "OnAppearing");
    await staffFacts;
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.IsVisible && label.Text == "Channel: mcp");
    InvokeLifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task MountedCommunitySectionRefetchesAfterStaffPromotion()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var publicDetail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    publicDetail["community"]!.AsObject().Remove("staff_provenance");
    var staffDetail = publicDetail.DeepClone();
    staffDetail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":null}
        """);
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        publicDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"),
        staffDetail.ToJsonString(), Fixture("web.communities.members.default"),
        Fixture("web.communities.posts.default"), Fixture("web.communities.list-items.counts.default"))), session);
    var page = new CommunityOverviewPage(model, session) { Slug = "test-community" };
    var publicLoad = WaitForCommunityLoadAsync(model);
    InvokeLifecycle(page, "OnAppearing");
    await publicLoad;
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page), label => label.IsVisible && label.Text == "Channel: mcp");

    var staffFacts = WaitForCommunityStaffFactsAsync(model);
    session.SetCurrent(new SessionSnapshot(new User("staff-1", "staff", Roles: ["moderator"])));
    await staffFacts;
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.IsVisible && label.Text == "Channel: mcp");
    InvokeLifecycle(page, "OnDisappearing");
  }

  private static Task WaitForResultsAsync(CommunityBrowseViewModel model, bool hasStaffFacts) =>
      WaitForPropertyAsync(model, nameof(model.Results), () =>
          model.Results.Count > 0 && model.Results.Any(row => (row.StaffProvenance is not null) == hasStaffFacts));

  private static Task WaitForCommunityLoadAsync(CommunityDetailViewModel model) =>
      WaitForPropertyAsync(model, nameof(model.Community), () => model.Community is not null);

  private static Task WaitForCommunityStaffFactsAsync(CommunityDetailViewModel model) =>
      WaitForPropertyAsync(model, nameof(model.Community), () => model.Community?.StaffProvenance is not null);

  private static Task WaitForPropertyAsync(INotifyPropertyChanged model, string property, Func<bool> ready)
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnChanged(object? sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == property && ready()) completion.TrySetResult();
    }
    model.PropertyChanged += OnChanged;
    if (ready()) completion.TrySetResult();
    return WaitAndDetachAsync();

    async Task WaitAndDetachAsync()
    {
      try
      {
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      }
      finally
      {
        model.PropertyChanged -= OnChanged;
      }
    }
  }
}
