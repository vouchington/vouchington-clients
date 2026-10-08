using Voucha.Client.Core.Api;
using Voucha.Client.Core.Growth;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.TopicRecommendations;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddTopicAndLandingPageServices(IServiceCollection services)
  {
    services.AddSingleton<ITopicsService, ApiTopicsService>();
    services.AddSingleton<ITopicRecommendationDetailService, ApiTopicRecommendationDetailService>();
    services.AddSingleton<ITopHashtagsService, ApiTopHashtagsService>();
    services.AddSingleton<IReferralLinksService, ApiReferralLinksService>();
    services.AddSingleton<ILandingPagesService, ApiLandingPagesService>();
    services.AddSingleton<IGrowthMetricsService, ApiGrowthMetricsService>();
    services.AddTransient<TopicsViewModel>();
    services.AddTransient<ReferralLinksViewModel>();
    services.AddTransient(sp => new LandingPagesViewModel(
        sp.GetRequiredService<ILandingPagesService>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>(),
        canViewAnalytics: false,
        fetchMembership: sp.GetRequiredService<VouchaApiClient>().FetchMembershipAsync));
    services.AddTransient<GrowthDashboardViewModel>();
    services.AddTransient<LandingPageAnalyticsViewModel>();
  }
}
