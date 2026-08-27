using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PointValuations;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  internal static void AddPointValuationServices(IServiceCollection services)
  {
    services.AddSingleton<IPointValuationsService, ApiPointValuationsService>();
    services.AddTransient(sp => new PointValuationsViewModel(
        sp.GetRequiredService<IPointValuationsService>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<PointValuationsPage>();
  }
}
