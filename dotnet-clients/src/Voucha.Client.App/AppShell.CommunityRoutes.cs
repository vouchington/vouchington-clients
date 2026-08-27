using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenCommunityRouteAsync(NativeDeepLinkResolution resolution)
  {
    return resolution.DestinationId switch
    {
      NativeRouteDestinationId.CommunitiesBrowse => Navigation.PushAsync(serviceProvider.GetRequiredService<CommunityBrowsePage>()),
      NativeRouteDestinationId.CommunityAction => OpenCommunityActionAsync(resolution),
      NativeRouteDestinationId.CommunityDetail => OpenCommunityDetailAsync(resolution),
      _ => null,
    };
  }

  private Task OpenCommunityDetailAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.Match?.Param("slug") is not { } slug)
    {
      return Task.CompletedTask;
    }

    var section = CommunityRouteSections.ForPath(resolution.Match.Path);
    var page = CreateCommunitySectionPage(section);
    page.Slug = slug;
    page.InitialModerationTransparencyRange = resolution.Match.QueryValue("range");
    if (ModmailThreadId(resolution.Match) is { } threadId)
    {
      page.InitialModmailThreadId = threadId;
    }
    return Navigation.PushAsync(page);
  }

  private static string? ModmailThreadId(NativeRouteMatch match)
  {
    if (match.Param("threadId") is { } threadId)
    {
      return threadId;
    }

    var segments = match.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var modmailIndex = Array.FindIndex(segments, segment => segment.Equals("modmail", StringComparison.OrdinalIgnoreCase));
    return modmailIndex >= 0 && modmailIndex + 1 < segments.Length ? segments[modmailIndex + 1] : null;
  }

  private CommunitySectionPage CreateCommunitySectionPage(CommunityDetailSurfaceSection section) =>
      section switch
      {
        CommunityDetailSurfaceSection.Members => serviceProvider.GetRequiredService<CommunityMembersPage>(),
        CommunityDetailSurfaceSection.Posts => serviceProvider.GetRequiredService<CommunityPostsPage>(),
        CommunityDetailSurfaceSection.News => serviceProvider.GetRequiredService<CommunityNewsPage>(),
        CommunityDetailSurfaceSection.Lists => serviceProvider.GetRequiredService<CommunityListsPage>(),
        CommunityDetailSurfaceSection.Settings => serviceProvider.GetRequiredService<CommunitySettingsPage>(),
        CommunityDetailSurfaceSection.PinnedPosts => serviceProvider.GetRequiredService<CommunityPinnedPostsPage>(),
        CommunityDetailSurfaceSection.Applications => serviceProvider.GetRequiredService<CommunityApplicationsPage>(),
        CommunityDetailSurfaceSection.Invites => serviceProvider.GetRequiredService<CommunityInvitesPage>(),
        CommunityDetailSurfaceSection.Bans => serviceProvider.GetRequiredService<CommunityBansPage>(),
        CommunityDetailSurfaceSection.Restrictions => serviceProvider.GetRequiredService<CommunityRestrictionsPage>(),
        CommunityDetailSurfaceSection.ModeratorVacation => serviceProvider.GetRequiredService<CommunityModeratorVacationPage>(),
        CommunityDetailSurfaceSection.AiAgents => serviceProvider.GetRequiredService<CommunityAiAgentsPage>(),
        CommunityDetailSurfaceSection.AgentPrompts => serviceProvider.GetRequiredService<CommunityAgentPromptsPage>(),
        CommunityDetailSurfaceSection.Moderation => serviceProvider.GetRequiredService<CommunityModerationPage>(),
        CommunityDetailSurfaceSection.Modlog => serviceProvider.GetRequiredService<CommunityModlogPage>(),
        CommunityDetailSurfaceSection.Modmail => serviceProvider.GetRequiredService<CommunityModmailPage>(),
        CommunityDetailSurfaceSection.ModerationAnalytics => serviceProvider.GetRequiredService<CommunityModerationAnalyticsPage>(),
        _ => serviceProvider.GetRequiredService<CommunityOverviewPage>(),
      };

  private Task OpenCommunityActionAsync(NativeDeepLinkResolution resolution)
  {
    var page = new CommunityActionPage(
        new CommunityActionViewModel(
            serviceProvider.GetRequiredService<ICommunitiesService>(),
            ActionKindForPath(resolution.Match?.Path ?? "/communities/create"),
            serviceProvider.GetRequiredService<AppConfig>(),
            serviceProvider.GetRequiredService<IUiLocalization>(),
            serviceProvider.GetRequiredService<IUiLocaleController>()),
        serviceProvider.GetRequiredService<ITurnstileTokenProvider>(),
        serviceProvider.GetRequiredService<VouchaApiClient>());

    if (resolution.Match?.Param("slug") is { } slug)
    {
      page.SetContext(slug);
    }

    if (resolution.Match?.Param("code") is { } code)
    {
      page.SetContext(inviteCode: code);
    }

    return Navigation.PushAsync(page);
  }

  private static CommunityActionKind ActionKindForPath(string path) =>
      path switch
      {
        var value when value.Equals("/communities/create", StringComparison.OrdinalIgnoreCase) => CommunityActionKind.Create,
        var value when value.EndsWith("/apply", StringComparison.OrdinalIgnoreCase) => CommunityActionKind.Apply,
        var value when value.Contains("/invite/", StringComparison.OrdinalIgnoreCase) => CommunityActionKind.RedeemInvite,
        _ => CommunityActionKind.Create,
      };

}
