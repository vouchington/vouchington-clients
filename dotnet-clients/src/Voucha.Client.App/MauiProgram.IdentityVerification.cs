using Voucha.Client.Core.IdentityVerificationAdministration;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddIdentityVerificationServices(IServiceCollection services)
  {
    services.AddSingleton<IIdentityVerificationAdministrationService, ApiIdentityVerificationAdministrationService>();
    services.AddTransient<IdentityVerificationAttemptGrantViewModel>();
  }
}
