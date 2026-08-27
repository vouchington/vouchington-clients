using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenPaymentCardsRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId != NativeRouteDestinationId.PaymentCards) return null;
    return MainThread.InvokeOnMainThreadAsync(() =>
        Navigation.PushAsync(serviceProvider.GetRequiredService<PaymentCardsPage>()));
  }
}
