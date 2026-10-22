using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Agent;
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
        ISessionStore sessionStore,
        AppConfig config)
    {
        InitializeComponent();
        this.serviceProvider = serviceProvider;
        this.localization = localization;
#if WINDOWS
        // Construct before navigation so sign-out/account-switch cleanup also works before chat opens.
        _ = serviceProvider.GetRequiredService<MemberMcpSession>();
#endif
        localeController.AttachSession(sessionStore);
        localeController.LocaleChanged += OnLocaleChanged;
        Resources["UiLocaleVersion"] = new UiLocaleVersion(localeController);
        Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(localization);
        Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(config, 96);
        RefreshLocalizedResources();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if DEBUG
        var preflight = CatalystGuiPreflight.CreateRoot();
        if (preflight is not null)
            return new Window(preflight);
#endif
        var window = new Window(serviceProvider.GetRequiredService<AppShell>());
        window.Activated += OnWindowActivated;
        return window;
    }

    private void OnWindowActivated(object? sender, EventArgs eventArgs) =>
        _ = serviceProvider.GetRequiredService<LocalizationRefreshService>().RefreshChromeAsync();

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
