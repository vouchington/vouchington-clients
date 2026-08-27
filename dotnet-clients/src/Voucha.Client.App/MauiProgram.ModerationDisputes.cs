using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddModerationDisputeServices(IServiceCollection services)
  {
    services.AddSingleton<IModerationDisputesService>(sp =>
        sp.GetRequiredService<ApiModerationService>());
    services.AddSingleton<ModerationDisputesPageFactory>();
  }
}
