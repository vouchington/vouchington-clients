using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public partial class App : Application
{
    private readonly IServiceProvider serviceProvider;
    private readonly IUiLocalization localization;

    internal IUiLocalization Localization => localization;

    public App(
        IServiceProvider serviceProvider,
        IUiLocalization localization,
        UiLocaleController localeController,
        ISessionStore sessionStore)
    {
        InitializeComponent();
        this.serviceProvider = serviceProvider;
        this.localization = localization;
        localeController.AttachSession(sessionStore);
        localeController.LocaleChanged += OnLocaleChanged;
        Resources["UiLocaleVersion"] = new UiLocaleVersion(localeController);
        Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(localization);
        RefreshLocalizedResources();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(serviceProvider.GetRequiredService<AppShell>());

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        AppLinkDispatcher.DispatchFireAndForget(uri);
    }

    private void OnLocaleChanged(object? sender, EventArgs eventArgs) =>
        Dispatcher.Dispatch(RefreshLocalizedResources);

    private void RefreshLocalizedResources()
    {
        foreach (var key in UiMessageKey.All)
        {
            Resources[key.Value] = localization.Localize(key);
        }
    }
}
