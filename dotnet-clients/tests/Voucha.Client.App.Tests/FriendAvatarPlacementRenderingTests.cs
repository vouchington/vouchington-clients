using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class FriendAvatarPlacementRenderingTests
{
  [Fact]
  public void FriendsPageKeepsInitialFallbackAndUsesOnlyTheResolvedPlacement()
  {
    InstallPageResources();
    var page = new FriendsPage(
        new FriendsViewModel(Proxy<IFriendsService>()),
        Proxy<ISessionStore>(),
        new FriendsRouteContextStore());
    var collection = Assert.Single(Descendants<CollectionView>(page));
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    var row = Row();
    card.BindingContext = row;

    var image = Assert.Single(Descendants<Image>(card));
    var initial = Assert.Single(Descendants<Label>(card), label => label.Text == "F");
    var name = Assert.Single(Descendants<Label>(card), label => label.Text == row.PrimaryLabel);
    Assert.Equal(ExpectedAvatarUrl, Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(initial));
    Assert.Equal(row.PrimaryLabel, name.Text);

    card.BindingContext = row with { ProfileImagePlacement = null };

    Assert.Null(image.Source);
    Assert.Equal("F", initial.Text);
  }

  [Fact]
  public void RecommendationPageRendersHydratedAvatarAndLeavesInitialWhenAbsent()
  {
    InstallPageResources();
    var page = new FriendRecommendationsPage(
        new FriendRecommendationsViewModel(Proxy<IFriendRecommendationsService>()));
    var collection = Assert.Single(Descendants<CollectionView>(page));
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    var row = new FriendRecommendationRow(
        new FriendRecommendation("user", "friend-1", OAuthBrokerProvider.Github, "Provider Name"),
        new User("friend-1", "friend", ProfileImagePlacement: Placement));
    card.BindingContext = row;

    var image = Assert.Single(Descendants<Image>(card));
    var initial = Assert.Single(Descendants<Label>(card), label => label.Text == "F");
    var name = Assert.Single(Descendants<Label>(card), label => label.Text == row.PrimaryLabel);
    Assert.Equal(ExpectedAvatarUrl, Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(initial));
    Assert.Equal(row.PrimaryLabel, name.Text);

    card.BindingContext = row with { User = new User("friend-1", "friend") };

    Assert.Null(image.Source);
    Assert.Equal("F", initial.Text);
  }

  [Fact]
  public void ProfileFriendsRowsKeepNavigationButtonAlongsideAvatarFallback()
  {
    InstallPageResources();
    var page = new ProfilePage();
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.AutomationId == "profile-friend-collection");
    var row = Assert.IsType<Grid>(collection.ItemTemplate.CreateContent());
    row.BindingContext = Row();

    var image = Assert.Single(Descendants<Image>(row));
    var label = Assert.Single(Descendants<Label>(row), item => item.Text == "F");
    var navigation = Assert.Single(Descendants<Button>(row), item => item.Text == "Friendly User");
    Assert.Equal(ExpectedAvatarUrl, Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(label));
    Assert.Equal("Friendly User", navigation.Text);
    Assert.IsType<FriendRow>(navigation.CommandParameter);

    row.BindingContext = Row() with { ProfileImagePlacement = null };

    Assert.Null(image.Source);
    Assert.Equal("F", label.Text);
  }

  private static readonly TopicImagePlacement Placement =
      new("00000000-0000-7000-8000-000000000201", "00000000-0000-7000-8000-000000000301", 2);

  private static readonly Uri ExpectedAvatarUrl = new(
      "https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96");

  private static FriendRow Row() => new(
      "friend-1",
      "Friendly User",
      null,
      "friend",
      "legacy-id",
      IsFollowing: true,
      IsSelf: false,
      ProfileImagePlacement: Placement);

  private static T Proxy<T>() where T : class => DispatchProxy.Create<T, UnusedService>();

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static void InstallPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new DeviceLanguage()));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
  }

  public class UnusedService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
  }

  private sealed class DeviceLanguage : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
