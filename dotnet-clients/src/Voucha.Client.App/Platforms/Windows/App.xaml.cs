using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Voucha.Client.App;
using Windows.ApplicationModel.Activation;

namespace Voucha.Client.App.WinUI;

public partial class App : MauiWinUIApplication
{
  private const string SingleInstanceKey = "Voucha.Client.App";

  public App()
  {
    if (RedirectSecondaryInstance())
    {
      return;
    }

    InitializeComponent();
    AppInstance.GetCurrent().Activated += OnActivated;
    DispatchActivation(AppInstance.GetCurrent().GetActivatedEventArgs());
  }

  protected override MauiApp CreateMauiApp() =>
      MauiProgram.CreateMauiApp();

  private static bool RedirectSecondaryInstance()
  {
    var currentInstance = AppInstance.GetCurrent();
    var primaryInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
    if (primaryInstance.IsCurrent)
    {
      return false;
    }

    primaryInstance.RedirectActivationToAsync(currentInstance.GetActivatedEventArgs())
        .AsTask()
        .GetAwaiter()
        .GetResult();
    Process.GetCurrentProcess().Kill();
    return true;
  }

  private static void OnActivated(object? sender, AppActivationArguments args)
  {
    DispatchActivation(args);
  }

  private static void DispatchActivation(AppActivationArguments args)
  {
    if (args.Kind != ExtendedActivationKind.Protocol ||
        args.Data is not IProtocolActivatedEventArgs protocolArgs)
    {
      return;
    }

    AppLinkDispatcher.DispatchFireAndForget(protocolArgs.Uri);
  }
}
