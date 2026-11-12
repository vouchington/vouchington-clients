using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddLocalizationServices(IServiceCollection services)
  {
    services.AddSingleton<IDeviceLanguageProvider>(_ =>
#if DEBUG
        CatalystAuthNewsLaunch.DeviceLanguages(new MauiDeviceLanguageProvider(), CatalystAuthNewsLaunch.IsRequested));
#else
        new MauiDeviceLanguageProvider());
#endif
    services.AddSingleton<IUiThreadDispatcher, MauiUiThreadDispatcher>();
    services.AddSingleton<UiLocaleController>();
    services.AddSingleton<IUiLocaleController>(sp => sp.GetRequiredService<UiLocaleController>());
    services.AddSingleton<LocalizationValueCache>();
    services.AddSingleton<IUiLocalization, UiLocalization>();
    services.AddSingleton<LocalizationRefreshService>();
  }
}
