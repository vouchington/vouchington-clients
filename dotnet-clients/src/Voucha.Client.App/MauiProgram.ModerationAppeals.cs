using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddModerationAppealServices(IServiceCollection services)
  {
    services.AddSingleton<IModerationAppealsService>(sp =>
        sp.GetRequiredService<ApiModerationService>());
    services.AddSingleton<AppealAIRerunAdmissionStore>();
    services.AddTransient(sp => new ModerationAppealsViewModel(
        sp.GetRequiredService<IModerationAppealsService>(),
        sp.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        sp.GetRequiredService<IUiLocaleController>(),
        sp.GetRequiredService<INavigationViewerProvider>(),
        sp.GetRequiredService<AppealAIRerunAdmissionStore>(),
        sp.GetRequiredService<AppConfig>().ApiBaseUrl,
        sp.GetRequiredService<IUiThreadDispatcher>()));
    services.AddTransient<ModerationAppealsPage>();
    services.AddSingleton<IMemberAppealsService, ApiMemberAppealsService>();
    services.AddSingleton<MemberAppealDraftStore>();
    services.AddTransient<MemberAppealsPageFactory>();
  }
}
