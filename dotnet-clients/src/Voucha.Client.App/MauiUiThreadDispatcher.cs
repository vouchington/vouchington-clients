using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed class MauiUiThreadDispatcher : IUiThreadDispatcher
{
  public bool IsDispatchRequired => !MainThread.IsMainThread;

  public void Dispatch(Action action)
  {
    ArgumentNullException.ThrowIfNull(action);
    if (!IsDispatchRequired)
    {
      action();
      return;
    }
    MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
  }
}
