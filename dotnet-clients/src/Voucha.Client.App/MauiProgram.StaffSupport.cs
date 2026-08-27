using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddStaffSupportServices(IServiceCollection services)
  {
    services.AddSingleton<IStaffSupportService, ApiStaffSupportService>();
    services.AddTransient<StaffSupportThreadsViewModel>();
    services.AddTransient<StaffSupportContactsViewModel>();
    services.AddTransient<StaffSupportContactViewModel>();
    services.AddTransient(sp => new StaffSupportThreadViewModel(
        sp.GetRequiredService<IStaffSupportService>(),
        sp.GetRequiredService<ISessionStore>().Current.Identity?.Id,
        sp.GetRequiredService<IUiLocalization>()));
  }
}
