using Microsoft.Maui.Dispatching;

namespace Voucha.Client.App.Tests;

internal sealed class ImmediateDispatcherProvider : IDispatcherProvider
{
  public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
}

internal sealed class ImmediateDispatcher : IDispatcher
{
  public static ImmediateDispatcher Instance { get; } = new();

  public bool IsDispatchRequired => false;

  public bool Dispatch(Action action)
  {
    action();
    return true;
  }

  public bool DispatchDelayed(TimeSpan delay, Action action)
  {
    action();
    return true;
  }

  // Tests whose controls start a dispatcher timer keep a local timer double.
  public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
}
