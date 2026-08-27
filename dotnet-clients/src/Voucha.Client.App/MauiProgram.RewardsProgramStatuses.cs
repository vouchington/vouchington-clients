using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  internal static void AddRewardsProgramStatusServices(IServiceCollection services)
  {
    services.AddSingleton<IRewardsProgramStatusesService, ApiRewardsProgramStatusesService>();
    services.AddTransient(sp => new RewardsProgramStatusesViewModel(sp.GetRequiredService<IRewardsProgramStatusesService>(), sp.GetRequiredService<IUiLocalization>(), sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<RewardsProgramStatusesPage>();
  }
}
