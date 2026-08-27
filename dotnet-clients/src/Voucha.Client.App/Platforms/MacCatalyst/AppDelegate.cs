using Foundation;
using UIKit;

namespace Voucha.Client.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
  protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

  public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
  {
    ArgumentNullException.ThrowIfNull(url);

    if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var uri))
    {
      AppLinkDispatcher.DispatchFireAndForget(uri);
      return true;
    }

    return false;
  }
}
