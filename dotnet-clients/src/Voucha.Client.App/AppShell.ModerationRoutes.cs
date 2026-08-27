using Voucha.Client.App.Pages;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateModerationPage(NativeRouteMatch? match)
  {
    if (MemberAppealsRoutes.TryResolve(match?.Path, out var memberRoute))
    {
      return serviceProvider.GetRequiredService<MemberAppealsPageFactory>().Create(memberRoute);
    }
    if (ModerationIntegrityRoutes.TryResolve(match?.Path, out var integrityRoute))
    {
      return IntegrityPage(integrityRoute);
    }
    if (ModerationRoutes.TryResolve(match, out var context))
    {
      if (context.RouteKind == ModerationRouteKind.Reports)
      {
        return serviceProvider.GetRequiredService<ModerationReportsPage>();
      }
      if (context.RouteKind == ModerationRouteKind.Appeals && !context.Mine)
      {
        return serviceProvider.GetRequiredService<ModerationAppealsPage>();
      }
      if (context.RouteKind == ModerationRouteKind.Disputes && !context.Mine)
      {
        return serviceProvider.GetRequiredService<ModerationDisputesPageFactory>().Create();
      }
      if (context.RouteKind == ModerationRouteKind.ReviewQueue)
      {
        return serviceProvider.GetRequiredService<ReviewQueuePage>();
      }
      var page = serviceProvider.GetRequiredService<ModerationPage>();
      page.SetContext(context);
      return page;
    }

    return new NavigationIntentPage(NavigationIntentViewModel.FromIntent(
        NavigationCatalog.All.Single(intent => intent.Id == "moderation"),
        viewerProvider.CurrentViewer,
        localization));
  }

  private async Task<bool> PrepareModerationIntentRouteMatchAsync(NativeRouteMatch match)
  {
    if (MemberAppealsRoutes.TryResolve(match.Path, out var memberRoute))
    {
      foreach (var content in EnumerateShellContents("moderation"))
      {
        var page = serviceProvider.GetRequiredService<MemberAppealsPageFactory>()
            .ReuseOrCreate(content.Content as MemberAppealsPage, memberRoute);
        var isExisting = ReferenceEquals(page, content.Content);
        ModerationDisputesPageLifetime.Replace(content, page);
        if (isExisting) await page.ReloadAsync().ConfigureAwait(true);
        else await page.EnsureLoadedAsync().ConfigureAwait(true);
        return false;
      }
      return true;
    }
    if (ModerationIntegrityRoutes.TryResolve(match.Path, out var integrityRoute))
    {
      foreach (var content in EnumerateShellContents("moderation"))
      {
        var page = IntegrityPage(integrityRoute);
        ModerationDisputesPageLifetime.Replace(content, page);
        await ReloadIntegrityPageAsync(page).ConfigureAwait(true);
        return false;
      }
      return true;
    }
    if (!ModerationRoutes.TryResolve(match, out var context))
    {
      return false;
    }

    foreach (var content in EnumerateShellContents("moderation"))
    {
      if (context.RouteKind == ModerationRouteKind.Reports)
      {
        var reportsPage = content.Content as ModerationReportsPage ??
            serviceProvider.GetRequiredService<ModerationReportsPage>();
        ModerationDisputesPageLifetime.Replace(content, reportsPage);
        await reportsPage.ReloadAsync().ConfigureAwait(true);
        return false;
      }

      if (context.RouteKind == ModerationRouteKind.Appeals && !context.Mine)
      {
        var appealsPage = content.Content as ModerationAppealsPage ??
            serviceProvider.GetRequiredService<ModerationAppealsPage>();
        ModerationDisputesPageLifetime.Replace(content, appealsPage);
        await appealsPage.ReloadAsync().ConfigureAwait(true);
        return false;
      }

      if (context.RouteKind == ModerationRouteKind.Disputes && !context.Mine)
      {
        var disputesPage = content.Content as ModerationDisputesPage ??
            serviceProvider.GetRequiredService<ModerationDisputesPageFactory>().Create();
        ModerationDisputesPageLifetime.Replace(content, disputesPage);
        await disputesPage.ReloadAsync().ConfigureAwait(true);
        return false;
      }

      if (context.RouteKind == ModerationRouteKind.ReviewQueue)
      {
        var reviewQueuePage = content.Content as ReviewQueuePage ??
            serviceProvider.GetRequiredService<ReviewQueuePage>();
        ModerationDisputesPageLifetime.Replace(content, reviewQueuePage);
        await reviewQueuePage.ReloadAsync().ConfigureAwait(true);
        return false;
      }

      var moderationPage = content.Content as ModerationPage ??
          serviceProvider.GetRequiredService<ModerationPage>();
      ModerationDisputesPageLifetime.Replace(content, moderationPage);
      await moderationPage.ApplyContextAsync(context).ConfigureAwait(true);
      return false;
    }

    return true;
  }

  private Page IntegrityPage(ModerationIntegrityRouteKind route) => route switch
  {
    ModerationIntegrityRouteKind.ReportFlags =>
        serviceProvider.GetRequiredService<ReportIntegrityPage>(),
    ModerationIntegrityRouteKind.ReportPenalties =>
        serviceProvider.GetRequiredService<ReportIntegrityPenaltiesPage>(),
    ModerationIntegrityRouteKind.VoteFlags =>
        serviceProvider.GetRequiredService<VoteIntegrityPage>(),
    ModerationIntegrityRouteKind.VotePenalties =>
        serviceProvider.GetRequiredService<VoteIntegrityPenaltiesPage>(),
    _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
  };

  private static Task ReloadIntegrityPageAsync(Page page) => page switch
  {
    ReportIntegrityPage report => report.ReloadAsync(),
    VoteIntegrityPage vote => vote.ReloadAsync(),
    ReportIntegrityPenaltiesPage reportPenalties => reportPenalties.ReloadAsync(),
    VoteIntegrityPenaltiesPage votePenalties => votePenalties.ReloadAsync(),
    _ => Task.CompletedTask,
  };
}
