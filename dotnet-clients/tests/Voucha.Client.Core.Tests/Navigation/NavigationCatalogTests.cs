using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NavigationCatalogTests
{
  [Fact]
  public void AnonymousBottomTabsMatchPublicClientIntents()
  {
    var tabs = BottomTabShellViewModel.Create(NavigationViewer.Anonymous).Tabs;

    Assert.Equal([
        "news",
        "podcasts",
        "videos",
        "posts",
        "topics",
        "referral-links",
        "web-search",
        "communities",
    ], tabs.Select(tab => tab.Id));
    Assert.All(tabs, tab => Assert.NotNull(tab.LandingHref));
  }

  [Fact]
  public void CatalogKeepsWebParityMetadata()
  {
    Assert.All(NavigationCatalog.All, intent =>
    {
      Assert.False(string.IsNullOrWhiteSpace(intent.Icon));
      Assert.All(intent.Groups, group =>
      {
        Assert.False(string.IsNullOrWhiteSpace(group.DataPw));
        Assert.All(group.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.DataPw)));
      });
    });
  }

  [Fact]
  public void SignedInViewerGetsAuthenticatedItemsWithoutStaffIntents()
  {
    var viewer = new NavigationViewer(true, []);
    var visibleIntentIds = NavigationCatalog.GetVisibleIntents(viewer).Select(intent => intent.Id);

    Assert.Contains("chat", visibleIntentIds);
    Assert.Contains("messages", visibleIntentIds);
    Assert.Contains("landing-pages", visibleIntentIds);
    Assert.Contains(NavigationCatalog.SettingsIntentId, visibleIntentIds);
    Assert.Contains("moderation", visibleIntentIds);
    Assert.DoesNotContain("crm", visibleIntentIds);
    Assert.DoesNotContain("engineering", visibleIntentIds);
    Assert.DoesNotContain("growth", visibleIntentIds);
  }

  [Fact]
  public void SignedInViewerGetsModerationTransparencyNavigation()
  {
    var viewer = new NavigationViewer(true, []);
    var intent = NavigationCatalog.GetVisibleIntents(viewer).Single(item => item.Id == "moderation");
    var hrefs = NavigationCatalog.GetVisibleGroups(intent, viewer)
        .SelectMany(group => group.Items)
        .Select(item => item.Href);

    Assert.Contains("/moderation-transparency", hrefs);
  }

  [Fact]
  public void SignedInBottomTabsIncludeAuthenticatedClientIntents()
  {
    var tabs = BottomTabShellViewModel.Create(new NavigationViewer(true, [])).Tabs;

    Assert.Contains(tabs, tab => tab.Id == "chat");
    Assert.Contains(tabs, tab => tab.Id == "messages");
    Assert.Contains(tabs, tab => tab.Id == "landing-pages");
    Assert.Contains(tabs, tab => tab.Id == "friends");
    Assert.Contains(tabs, tab => tab.Id == "lists");
  }

  [Fact]
  public void LandingPagesIntentResolvesToNativeManagementRoute()
  {
    var viewer = new NavigationViewer(true, []);
    var intent = NavigationCatalog.All.Single(item => item.Id == "landing-pages");
    var viewModel = NavigationIntentViewModel.FromIntent(intent, viewer);

    Assert.Equal("/my/landing-pages", viewModel.LandingHref);
  }

  [Fact]
  public void EngineeringIntentExposesNativeOpsRoutes()
  {
    var viewer = new NavigationViewer(true, ["administrator"]);
    var intent = NavigationCatalog.All.Single(item => item.Id == "engineering");
    var viewModel = NavigationIntentViewModel.FromIntent(intent, viewer);
    var hrefs = viewModel.Groups.SelectMany(group => group.Items).Select(item => item.Href).ToArray();

    Assert.Contains("/admin/queues", hrefs);
    Assert.Contains("/admin/postgresql", hrefs);
    Assert.Contains("/admin/valkey", hrefs);
  }

  [Theory]
  [InlineData("moderator")]
  [InlineData("developer")]
  [InlineData("customer_support")]
  [InlineData("investor")]
  public void EngineeringViewerRolesSeeOnlyDynamicConfig(string role)
  {
    var viewer = new NavigationViewer(true, [role]);
    var intent = NavigationCatalog.GetVisibleIntents(viewer).Single(item => item.Id == "engineering");
    var groups = NavigationCatalog.GetVisibleGroups(intent, viewer);

    var group = Assert.Single(groups);
    Assert.Equal("Dynamic Config", UiLocalization.English.Localize(group.LabelKey));
    Assert.Equal("/admin/dynamic-config", Assert.Single(group.Items).Href);
  }

  [Fact]
  public void MutableViewerProviderCanMoveShellFromAnonymousToSignedInRoles()
  {
    var provider = new MutableNavigationViewerProvider();
    var observedViewers = new List<NavigationViewer>();
    provider.ViewerChanged += (_, args) => observedViewers.Add(args.Viewer);

    Assert.False(provider.CurrentViewer.IsAuthenticated);

    provider.SetViewer(new NavigationViewer(true, ["administrator"]));
    var tabs = BottomTabShellViewModel.Create(provider.CurrentViewer).Tabs;

    Assert.True(provider.CurrentViewer.IsAuthenticated);
    Assert.Contains("administrator", provider.CurrentViewer.Roles);
    Assert.Contains(tabs, tab => tab.Id == "chat");
    Assert.Single(observedViewers);
    Assert.Equal(provider.CurrentViewer, observedViewers[0]);

    provider.SetViewer(provider.CurrentViewer);

    Assert.Single(observedViewers);
  }

  [Fact]
  public void ViewerIdentityAccessDistinguishesAccountsWithTheSameRoles()
  {
    var first = NavigationCatalog.FromIdentity(new User("user-1", "alice", Roles: ["developer"]));
    var second = NavigationCatalog.FromIdentity(new User("user-2", "bob", Roles: ["developer"]));

    Assert.Equal("user-1", first.IdentityId);
    Assert.False(first.HasSameIdentityAccess(second));
    Assert.True(first.HasSameIdentityAccess(first with { FeatureFlags = new Dictionary<string, bool>() }));
  }

  [Fact]
  public void AdministratorViewerGetsStaffIntents()
  {
    var viewer = new NavigationViewer(true, ["administrator"]);
    var visibleIntentIds = NavigationCatalog.GetVisibleIntents(viewer).Select(intent => intent.Id);

    Assert.Contains("moderation", visibleIntentIds);
    Assert.Contains("crm", visibleIntentIds);
    Assert.Contains("engineering", visibleIntentIds);
    Assert.Contains("growth", visibleIntentIds);
  }

  [Fact]
  public void AdministratorViewerGetsStaffIntentsOutsideBottomTabs()
  {
    var viewer = new NavigationViewer(true, ["administrator"]);
    var nonBottomIntentIds = NavigationCatalog.GetVisibleNonBottomIntents(viewer).Select(intent => intent.Id);

    Assert.Contains("moderation", nonBottomIntentIds);
    Assert.Contains("crm", nonBottomIntentIds);
    Assert.Contains("engineering", nonBottomIntentIds);
    Assert.Contains("growth", nonBottomIntentIds);
    Assert.DoesNotContain("news", nonBottomIntentIds);
  }

  [Fact]
  public void InvestorViewerGetsGrowthAndReadOnlyDynamicConfig()
  {
    var viewer = new NavigationViewer(true, ["investor"]);
    var visibleIntentIds = NavigationCatalog.GetVisibleIntents(viewer).Select(intent => intent.Id);

    Assert.Contains("growth", visibleIntentIds);
    Assert.DoesNotContain("crm", visibleIntentIds);
    Assert.Contains("engineering", visibleIntentIds);
    var engineering = NavigationCatalog.All.Single(intent => intent.Id == "engineering");
    var groups = NavigationCatalog.GetVisibleGroups(engineering, viewer);
    Assert.Single(groups);
    Assert.Equal("Dynamic Config", UiLocalization.English.Localize(groups[0].LabelKey));
  }

  [Fact]
  public void ViewerWithNullRolesDoesNotSeeRoleRestrictedNavigation()
  {
    var viewer = new NavigationViewer(true, null!);
    var visibleIntentIds = NavigationCatalog.GetVisibleIntents(viewer).Select(intent => intent.Id);

    Assert.Contains("chat", visibleIntentIds);
    Assert.DoesNotContain("growth", visibleIntentIds);
    Assert.DoesNotContain("crm", visibleIntentIds);
    Assert.DoesNotContain("engineering", visibleIntentIds);
  }

  [Fact]
  public void SettingsIntentExposesAccountAndPrivacyRoutes()
  {
    var viewer = new NavigationViewer(true, []);
    var settingsIntent = NavigationCatalog.GetVisibleIntents(viewer)
        .Single(intent => intent.Id == NavigationCatalog.SettingsIntentId);
    var hrefs = settingsIntent.Groups.SelectMany(group => group.Items).Select(item => item.Href).ToArray();

    Assert.Contains("/my/identity", hrefs);
    Assert.Contains("/my/profile", hrefs);
    Assert.Contains("/my/privacy", hrefs);
    Assert.Contains("/my/membership", hrefs);
    Assert.Contains("/my/notification-settings", hrefs);
    Assert.Contains("/my/api-keys", hrefs);
    Assert.Contains("/my/data", hrefs);
  }

  [Theory]
  [InlineData("en", "News", "Settings", "Engineering", "Financial")]
  [InlineData("es", "Noticias", "Configuración", "Ingeniería", "Finanzas")]
  [InlineData("fr", "Actualités", "Paramètres", "Ingénierie", "Finances")]
  [InlineData("pt", "Notícias", "Configurações", "Engenharia", "Finanças")]
  public void VisibleCatalogResolvesAllAssignedGroupsFromTheActiveLocale(
      string locale,
      string expectedNews,
      string expectedSettings,
      string expectedEngineering,
      string expectedFinancial)
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var viewer = new NavigationViewer(true, ["administrator"]);

    controller.ApplySavedLocale(locale);
    var localized = NavigationCatalog.GetVisibleIntents(viewer)
        .Select(intent => NavigationIntentViewModel.FromIntent(intent, viewer, localization))
        .ToArray();

    Assert.Equal(expectedNews, localized.Single(intent => intent.Id == "news").Label);
    Assert.Equal(expectedSettings, localized.Single(intent => intent.Id == NavigationCatalog.SettingsIntentId).Label);
    Assert.Equal(expectedEngineering, localized.Single(intent => intent.Id == "engineering").Label);
    Assert.Contains(
        expectedFinancial,
        localized.Single(intent => intent.Id == NavigationCatalog.SettingsIntentId)
            .Groups.Select(group => group.Label));
  }

  [Fact]
  public void RebuildingVisibleCatalogAfterLocaleChangedRefreshesLabels()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var viewer = new NavigationViewer(true, []);
    var rebuildCount = 0;
    IReadOnlyList<NavigationIntentViewModel> visible = [];
    void Rebuild()
    {
      rebuildCount++;
      visible = NavigationCatalog.GetVisibleIntents(viewer)
          .Select(intent => NavigationIntentViewModel.FromIntent(intent, viewer, localization))
          .ToArray();
    }

    controller.LocaleChanged += (_, _) => Rebuild();
    Rebuild();
    Assert.Equal("Settings", visible.Single(intent => intent.Id == NavigationCatalog.SettingsIntentId).Label);

    controller.ApplySavedLocale("fr");

    Assert.Equal(2, rebuildCount);
    Assert.Equal("Paramètres", visible.Single(intent => intent.Id == NavigationCatalog.SettingsIntentId).Label);
    Assert.Equal("Actualités", visible.Single(intent => intent.Id == "news").Label);
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
