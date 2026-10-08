using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddLocalizationServices(IServiceCollection services)
  {
    services.AddSingleton<IDeviceLanguageProvider, MauiDeviceLanguageProvider>();
    services.AddSingleton<IUiThreadDispatcher, MauiUiThreadDispatcher>();
    services.AddSingleton<UiLocaleController>();
    services.AddSingleton<IUiLocaleController>(sp => sp.GetRequiredService<UiLocaleController>());
    services.AddSingleton<LocalizationValueCache>();
    services.AddSingleton<IUiLocalization, UiLocalization>();
    services.AddSingleton<LocalizationRefreshService>();
  }
}
