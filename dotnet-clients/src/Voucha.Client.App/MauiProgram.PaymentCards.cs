using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PaymentCards;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  internal static void AddPaymentCardServices(IServiceCollection services)
  {
    services.AddSingleton<IPaymentCardsService, ApiPaymentCardsService>();
    services.AddTransient(sp => new PaymentCardsViewModel(
        sp.GetRequiredService<IPaymentCardsService>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    services.AddTransient<PaymentCardsPage>();
  }
}
