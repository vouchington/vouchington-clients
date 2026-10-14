using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityPlacementRenderingTests
{
  [Fact]
  public async Task OverviewRendersOnlyRevisionBoundCommunityPlacements()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var detail = Fixture("web.communities.show.default");
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        detail, Fixture("web.communities.members.default"), Fixture("web.communities.posts.default"),
        Fixture("web.communities.list-items.counts.default"))));
    var page = new CommunityOverviewPage(model, new AnonymousSessionStore(), Config());

    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);

    var images = Descendants<Image>(page).ToDictionary(image => image.AutomationId);
    Assert.Equal(new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000305/5/00000000-0000-7000-8000-000000000204?w=960"),
        Assert.IsType<UriImageSource>(images["community-banner-image"].Source).Uri);
    Assert.Equal(new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000304/2/00000000-0000-7000-8000-000000000203?w=96"),
        Assert.IsType<UriImageSource>(images["community-profile-image"].Source).Uri);
    Assert.True(images["community-banner-image"].IsVisible);
    Assert.True(images["community-profile-image"].IsVisible);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(images["community-profile-image"]));
    Assert.Contains(Descendants<Label>(page), label => label.Text == "Test Community");
  }

  [Fact]
  public async Task MissingCommunityPlacementsKeepTheNameAndHideRemoteImages()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var detail = JsonNode.Parse(Fixture("web.communities.show.default"))!;
    detail["community"]!["profile_image_placement"] = null;
    detail["community"]!["banner_image_placement"] = null;
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        detail.ToJsonString(), Fixture("web.communities.members.default"), Fixture("web.communities.posts.default"),
        Fixture("web.communities.list-items.counts.default"))));
    var page = new CommunityOverviewPage(model, new AnonymousSessionStore(), Config());

    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);

    var images = Descendants<Image>(page).ToDictionary(image => image.AutomationId);
    Assert.Null(images["community-banner-image"].Source);
    Assert.Null(images["community-profile-image"].Source);
    Assert.False(images["community-banner-image"].IsVisible);
    Assert.False(images["community-profile-image"].IsVisible);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(images["community-profile-image"]));
    Assert.Contains(Descendants<Label>(page), label => label.Text == "Test Community");
  }

  [Fact]
  public void BrowseCardsUsePlacementIdentityAndHideMissingAvatars()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var page = new CommunityBrowsePage(
        new CommunityBrowseViewModel(new ApiCommunitiesService(Client("{}"))),
        new AnonymousSessionStore(), Config());
    var collection = Assert.Single(Descendants<CollectionView>(page));
    var card = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    var avatar = Assert.Single(Descendants<Image>(card), image => image.AutomationId == "community-profile-image");
    var row = new CommunityBrowseRow("community-1", "Community", "community", 0, 0, false,
        ProfileImagePlacement: new TopicImagePlacement("avatar-1", "profile-placement", 7));

    card.BindingContext = row;

    Assert.Equal(new Uri("https://images.test/images/placements/profile-placement/7/avatar-1?w=96"),
        Assert.IsType<UriImageSource>(avatar.Source).Uri);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(avatar));
    Assert.Contains(Descendants<Label>(card), label => label.Text == "Community");
    card.BindingContext = row with { ProfileImagePlacement = null };
    Assert.Null(avatar.Source);
    Assert.False(avatar.IsVisible);
  }

  [Fact]
  public async Task MemberRowsRenderOnlyThePublicUsersCanonicalPlacement()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var responses = new Dictionary<string, string>
    {
      ["/api/v1/communities/test-community"] = Fixture("web.communities.show.default"),
      ["/api/v1/communities/test-community/members"] = Fixture("web.communities.members.default"),
      ["/api/v1/communities/test-community/posts"] = Fixture("web.communities.posts.default"),
      ["/api/v1/communities/test-community/list-items/counts"] = Fixture("web.communities.list-items.counts.default"),
    };
    var apiClient = new VouchaApiClient(new HttpClient(new RouteResponseHandler(responses))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(apiClient));
    var page = new CommunityMembersPage(model, new AnonymousSessionStore(), Config());

    await model.LoadSurfaceAsync("test-community", CommunityDetailSurfaceSection.Members,
        TestContext.Current.CancellationToken);

    var collection = Assert.IsType<CollectionView>(typeof(CommunitySectionPage)
        .GetField("rowsView", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(page));
    var member = Assert.IsType<CommunitySummaryRow>(Assert.Single((IEnumerable<object>)collection.ItemsSource!));
    var row = Assert.IsType<Grid>(collection.ItemTemplate!.CreateContent());
    row.BindingContext = member;
    var avatar = Assert.Single(Descendants<Border>(row), border => border.AutomationId == "community-member-avatar");
    var avatarImage = Assert.Single(Descendants<Image>(avatar), image => image.AutomationId == "community-member-avatar-image");

    Assert.Equal("Test User", member.Title);
    Assert.Equal("Owner", member.Subtitle);
    Assert.Equal(new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"),
        Assert.IsType<UriImageSource>(avatarImage.Source).Uri);
    Assert.True(avatar.IsVisible);
    Assert.IsType<Microsoft.Maui.Controls.Shapes.Ellipse>(avatar.StrokeShape);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(avatar));
    Assert.False(AutomationProperties.GetIsInAccessibleTree(avatarImage));
    Assert.Equal(new GridLength(40), row.ColumnDefinitions[0].Width);
    Assert.Equal(GridLength.Star, row.ColumnDefinitions[1].Width);
    Assert.Equal(8d, row.ColumnSpacing);
    var title = Assert.Single(Descendants<Label>(row), label => label.Text == "Test User");
    Assert.Equal(LineBreakMode.WordWrap, title.LineBreakMode);

    row.BindingContext = member with
    {
      ProfileImagePlacement = new TopicImagePlacement("avatar-1", "", 2),
    };
    Assert.Null(avatarImage.Source);
    Assert.False(avatar.IsVisible);
    Assert.Equal(new GridLength(0), row.ColumnDefinitions[0].Width);
    Assert.Equal(0d, row.ColumnSpacing);
    row.BindingContext = member with { ProfileImagePlacement = null };
    Assert.Null(avatarImage.Source);
    Assert.False(avatar.IsVisible);
    Assert.Equal(new GridLength(0), row.ColumnDefinitions[0].Width);
    Assert.Equal(0d, row.ColumnSpacing);
  }

  [Fact]
  public void NonMemberRowsDoNotConstructMemberAvatarControls()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client("{}")));
    var page = new CommunityPostsPage(model, new AnonymousSessionStore(), Config());
    var collection = Assert.IsType<CollectionView>(typeof(CommunitySectionPage)
        .GetField("rowsView", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(page));

    var row = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate!.CreateContent());

    Assert.DoesNotContain(Descendants<Image>(row), image => image.AutomationId == "community-member-avatar-image");
    Assert.DoesNotContain(Descendants<Border>(row), border => border.AutomationId == "community-member-avatar");
  }

  private static AppConfig Config() => new(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test"));

  private static string Fixture(string id)
  {
    var manifest = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")))!;
    var path = manifest["fixtures"]!.AsArray().Single(entry => (string?)entry?["id"] == id)!["bodyFile"]!.GetValue<string>();
    return File.ReadAllText(FilamentsContractPaths.ApiFixture(path));
  }

  private static VouchaApiClient Client(params string[] bodies) =>
      new(new HttpClient(new ResponseHandler(bodies)) { BaseAddress = new Uri("https://api.test") });

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

  private sealed class RouteResponseHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri?.AbsolutePath ?? "";
      if (!responses.TryGetValue(path, out var body))
      {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
          RequestMessage = request,
        });
      }
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      });
    }
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
