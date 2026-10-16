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

  // Timer-backed page tests use ImmediateTimerDispatcherProvider instead.
  public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
}
