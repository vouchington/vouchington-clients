using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SourceCreation;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnAddSourceClicked(object? sender, EventArgs args) => await AddSourceAsync();

  public async Task AddSourceAsync()
  {
    if (!sessionStore.Current.IsAuthenticated) return;
    var service = new SourceCreationService(serviceProvider.GetRequiredService<VouchaApiClient>(),
        serviceProvider.GetRequiredService<HttpClient>());
    var model = new SourceCreationViewModel(service, sessionStore,
        serviceProvider.GetRequiredService<IUiLocalization>(),
        localeController: serviceProvider.GetService<IUiLocaleController>());
    await Navigation.PushAsync(new SourceCreationPage(model,
        slug => serviceProvider.GetRequiredService<AppShell>().OpenNativePathAsync(NativeRoutePath.Entity("source", slug))));
  }
}
