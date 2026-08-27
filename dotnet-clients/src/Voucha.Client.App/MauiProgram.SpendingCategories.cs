using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SpendingCategories;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  internal static void AddSpendingCategoryServices(IServiceCollection services)
  {
    services.AddSingleton<ISpendingCategoriesService, ApiSpendingCategoriesService>();
    services.AddTransient(sp => new SpendingCategoriesViewModel(sp.GetRequiredService<ISpendingCategoriesService>(),
        sp.GetRequiredService<IUiLocalization>(), sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<SpendingCategoriesPage>();
  }
}
