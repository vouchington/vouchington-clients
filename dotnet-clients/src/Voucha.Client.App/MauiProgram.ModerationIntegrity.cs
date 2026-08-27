using Voucha.Client.App.Pages;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddModerationIntegrityServices(IServiceCollection services)
  {
    services.AddSingleton<IModerationIntegrityService, ApiModerationIntegrityService>();
    services.AddTransient(serviceProvider => new ReportIntegrityViewModel(
        serviceProvider.GetRequiredService<IModerationIntegrityService>(),
        serviceProvider.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
    services.AddTransient(serviceProvider => new VoteIntegrityViewModel(
        serviceProvider.GetRequiredService<IModerationIntegrityService>(),
        serviceProvider.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
    services.AddTransient(serviceProvider => new ReportIntegrityPenaltyViewModel(
        serviceProvider.GetRequiredService<IModerationIntegrityService>(),
        serviceProvider.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
    services.AddTransient(serviceProvider => new VoteIntegrityPenaltyViewModel(
        serviceProvider.GetRequiredService<IModerationIntegrityService>(),
        serviceProvider.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<ReportIntegrityPage>();
    services.AddTransient<VoteIntegrityPage>();
    services.AddTransient<ReportIntegrityPenaltiesPage>();
    services.AddTransient<VoteIntegrityPenaltiesPage>();
  }
}
