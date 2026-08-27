namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  public const string NewsIntentId = "news";
  public const string SettingsIntentId = "settings";
  public const string ListsIntentId = "lists";
  public const string MessagesIntentId = "messages";

  private static readonly string[] BottomTabIds =
  [
    NewsIntentId,
    "podcasts",
    "videos",
    "posts",
    "topics",
    "referral-links",
    "web-search",
    "fediverse",
    "chat",
    "messages",
    "landing-pages",
    "communities",
    "friends",
    ListsIntentId,
  ];

  public static IReadOnlyList<NavIntent> All { get; }

  public static IReadOnlyList<NavIntent> BottomTabs { get; }

  public static IReadOnlyList<NavIntent> GetVisibleIntents(NavigationViewer viewer) =>
      All.Where(intent => IsVisible(intent, viewer)).ToArray();

  public static IReadOnlyList<NavIntent> GetVisibleBottomTabs(NavigationViewer viewer) =>
      BottomTabs.Where(intent => IsVisible(intent, viewer)).ToArray();

  public static IReadOnlyList<NavIntent> GetVisibleNonBottomIntents(NavigationViewer viewer)
  {
    return GetVisibleIntents(viewer)
        .Where(intent => !BottomTabIdSet.Contains(intent.Id))
        .ToArray();
  }

  public static bool IsVisible(NavIntent intent, NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(intent);
    ArgumentNullException.ThrowIfNull(viewer);

    if (intent.RequiresAuth && !viewer.IsAuthenticated)
    {
      return false;
    }

    if (intent.FeatureFlag is { Length: > 0 } featureFlag &&
        (viewer.FeatureFlags?.TryGetValue(featureFlag, out var enabled) != true || !enabled))
    {
      return false;
    }

    return IsVisible(intent.RequiresAuth, intent.Roles, viewer);
  }

  public static bool IsVisible(NavGroup group, NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(group);
    ArgumentNullException.ThrowIfNull(viewer);

    if (group.RequiresAuth && !viewer.IsAuthenticated)
    {
      return false;
    }

    return IsVisible(group.RequiresAuth, group.Roles, viewer);
  }

  public static bool IsVisible(NavItem item, NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(viewer);

    return !item.ComingSoon && (!item.RequiresAuth || viewer.IsAuthenticated);
  }

  public static IReadOnlyList<NavGroup> GetVisibleGroups(NavIntent intent, NavigationViewer viewer) =>
      IsVisible(intent, viewer)
          ? intent.Groups.Where(group => IsVisible(group, viewer)).ToArray()
          : Array.Empty<NavGroup>();

  public static IReadOnlyList<NavItem> GetVisibleItems(NavGroup group, NavigationViewer viewer) =>
      IsVisible(group, viewer)
          ? group.Items.Where(item => IsVisible(item, viewer)).ToArray()
          : Array.Empty<NavItem>();

  public static string? FindLandingHref(NavIntent intent, NavigationViewer viewer)
  {
    ArgumentNullException.ThrowIfNull(intent);
    ArgumentNullException.ThrowIfNull(viewer);

    if (!IsVisible(intent, viewer))
    {
      return null;
    }

    foreach (var group in intent.Groups)
    {
      if (!IsVisible(group, viewer))
      {
        continue;
      }

      foreach (var item in group.Items)
      {
        if (IsVisible(item, viewer))
        {
          return item.Href;
        }
      }
    }

    return null;
  }

  public static NavigationViewer FromIdentity(
      Voucha.Client.Core.Api.User? identity,
      IReadOnlyDictionary<string, bool>? featureFlags = null) =>
      identity is null
          ? new NavigationViewer(false, Array.Empty<string>(), featureFlags)
          : new NavigationViewer(true, identity.Roles ?? [], featureFlags, identity.Id);

  static NavigationCatalog()
  {
    All =
    [
      NewsIntent,
      PodcastsIntent,
      VideosIntent,
      PostsIntent,
      TopicsIntent,
      ReferralLinksIntent,
      WebSearchIntent,
      FediverseIntent,
      ChatIntent,
      MessagesIntent,
      LandingPagesIntent,
      CommunitiesIntent,
      FriendsIntent,
      ListsIntent,
      SettingsIntent,
      ModerationIntent,
      CrmIntent,
      EngineeringIntent,
      GrowthIntent,
    ];

    BottomTabs = All.Where(intent => BottomTabIds.Contains(intent.Id, StringComparer.Ordinal)).ToArray();
  }

  private static readonly HashSet<string> BottomTabIdSet = new(BottomTabIds, StringComparer.Ordinal);

  private static bool IsVisible(bool requiresAuth, IReadOnlyList<string>? roles, NavigationViewer viewer)
  {
    if (requiresAuth && !viewer.IsAuthenticated)
    {
      return false;
    }

    if (roles is not { Count: > 0 } requiredRoles)
    {
      return true;
    }

    return viewer.Roles is { Count: > 0 } viewerRoles &&
        requiredRoles.Any(role => viewerRoles.Contains(role, StringComparer.Ordinal));
  }
}
