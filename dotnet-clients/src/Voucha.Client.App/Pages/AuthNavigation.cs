using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

internal static class AuthNavigation
{
  public static async Task<bool> EnsureSignedInAsync(
      this Page page,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider)
  {
    if (sessionStore.Current.IsAuthenticated) return true;
    await page.Navigation.PushAsync(serviceProvider.GetRequiredService<AuthPage>());
    return false;
  }
}
