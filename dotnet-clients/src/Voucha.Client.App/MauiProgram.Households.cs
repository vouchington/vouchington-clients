using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddHouseholdServices(IServiceCollection services)
  {
    services.AddSingleton<IHouseholdService, ApiHouseholdService>();
    services.AddTransient(sp => new HouseholdViewModel(
        sp.GetRequiredService<IHouseholdService>(),
        sp.GetRequiredService<ISessionStore>().Current.Identity?.Id ??
            throw new InvalidOperationException("Household management requires an authenticated user."),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<HouseholdPage>();
  }
}
