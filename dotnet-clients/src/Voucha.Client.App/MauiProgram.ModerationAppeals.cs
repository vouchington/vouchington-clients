using Voucha.Client.App.Pages;
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
    services.AddTransient(sp => new ModerationAppealsViewModel(
        sp.GetRequiredService<IModerationAppealsService>(),
        sp.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<ModerationAppealsPage>();
    services.AddSingleton<IMemberAppealsService, ApiMemberAppealsService>();
    services.AddSingleton<MemberAppealDraftStore>();
    services.AddTransient<MemberAppealsPageFactory>();
  }
}
