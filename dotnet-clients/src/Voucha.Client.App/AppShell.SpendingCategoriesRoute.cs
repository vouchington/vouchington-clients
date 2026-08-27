using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenSpendingCategoriesRouteAsync(NativeDeepLinkResolution resolution) =>
      resolution.DestinationId == NativeRouteDestinationId.SpendingCategories
          ? MainThread.InvokeOnMainThreadAsync(() => Navigation.PushAsync(serviceProvider.GetRequiredService<SpendingCategoriesPage>()))
          : null;
}
