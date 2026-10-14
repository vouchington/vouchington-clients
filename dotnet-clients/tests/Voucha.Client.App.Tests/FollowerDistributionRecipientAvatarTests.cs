using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class FollowerDistributionRecipientAvatarTests
{
  [Fact]
  public async Task SendPageRendersCanonicalPlacementAndRetainsAvatarInSelectedRecipients()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var fixture = JsonSerializer.Deserialize<UserFollowersResponse>(
        File.ReadAllText(FilamentsContractPaths.ApiFixture("responses/swift.users.followers.default.json")),
        VouchaApiJson.Options)!;
    // The claimed follower fixture currently has no placement. Add the optional API field
    // locally to one fixture-derived user to exercise the rendered placement path.
    var fixtureUsers = fixture.Results.ToArray();
    fixtureUsers[0] = fixtureUsers[0] with
    {
      ProfileImagePlacement = new TopicImagePlacement(
        "00000000-0000-7000-8000-000000000201",
        "00000000-0000-7000-8000-000000000301",
        2)
    };
    var users = fixtureUsers.Concat([
      new User("01900000-0000-7000-8000-000000000002", "bob", Name: "🌱 Bob", ProfileImageId: "legacy-only")
    ]).ToArray();
    using var state = new FollowerDistributionViewModel(
        new FriendsService(users),
        new VouchaApiClient(new HttpClient()),
        FollowerDistributionTargetKind.Post,
        "post-1");
    state.SetAudience(true);
    const string viewerId = "01900000-0000-7000-8000-000000000099";
    var config = new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test"));
    var page = new FollowerDistributionSendPage(state, viewerId, new SessionStore(), config);

    await state.SearchFollowersAsync(viewerId, "", TestContext.Current.CancellationToken);
    typeof(FollowerDistributionSendPage)
        .GetMethod("Refresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(page, null);

    var images = Descendants<Image>(page).Where(image => image.AutomationId == "follower-recipient-avatar-image").ToArray();
    Assert.Equal(2, images.Length);
    Assert.Equal(
        new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"),
        Assert.IsType<UriImageSource>(images[0].Source).Uri);
    Assert.Null(images[1].Source);
    Assert.Contains(Descendants<Label>(page), label => label.Text == "friend");
    Assert.Contains(Descendants<Label>(page), label => label.Text == "🌱 Bob");
    var initial = Assert.Single(Descendants<Label>(page), label =>
        label.AutomationId == "follower-recipient-avatar-initial" && label.Text == "🌱");
    Assert.False(AutomationProperties.GetIsInAccessibleTree(initial));
    var existingRemoteAvatar = Assert.Single(images, image => image.Source is UriImageSource);
    var existingFallbackAvatar = Assert.Single(images, image => image.Source is null);

    var checkbox = Assert.Single(Descendants<CheckBox>(page), item =>
        item.AutomationId == "follower-recipient-checkbox-friend-1");
    checkbox.IsChecked = true;
    var selected = Assert.Single(Descendants<VerticalStackLayout>(page), layout =>
        layout.AutomationId == "follower-distribution-selected-recipients");
    var selectedImage = Assert.Single(Descendants<Image>(selected));
    Assert.Equal(
        new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"),
        Assert.IsType<UriImageSource>(selectedImage.Source).Uri);
    var results = Assert.Single(Descendants<VerticalStackLayout>(page), layout =>
        layout.AutomationId == "follower-distribution-recipient-results");
    Assert.Same(existingFallbackAvatar, Assert.Single(
        Descendants<Image>(results), image => image.Source is null));
    Assert.Same(existingRemoteAvatar, Assert.Single(
        Descendants<Image>(results), image => image.Source is UriImageSource));
    Assert.Equal(new[] { "friend-1" }, state.SelectedIds);
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private sealed class FriendsService(IReadOnlyList<User> users) : IFriendsService
  {
    public Task<UserFollowersResponse> FetchFollowersAsync(
        string userId,
        string? query = null,
        string? after = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserFollowersResponse(users, new PageInfo(null, false, null)));

    public Task<UserFollowingResponse> FetchFollowingAsync(
        string userId,
        string? after = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SetFollowAsync(string userId, bool following, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
