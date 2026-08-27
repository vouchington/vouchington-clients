using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public sealed partial class NativeHtmlContentView
{
  private IDisposable? localeSubscription;

  protected override void OnHandlerChanged()
  {
    base.OnHandlerChanged();
    localeSubscription?.Dispose();
    localeSubscription = Handler?.MauiContext?.Services
        .GetService<IUiLocaleController>()
        ?.SubscribeLocaleChanges(this);
  }

  public void OnUiLocaleChanged() => Render();
}
