using Microsoft.Maui.Dispatching;

namespace Voucha.Client.App.Tests;

internal enum ImmediateTimerStart
{
  // Start invokes Tick once, then IsRunning is false even when IsRepeating is true.
  StopAfterTick,

  // Start invokes Tick once, then IsRunning stays true only when IsRepeating is true.
  RemainRunningWhenRepeating,
}

internal sealed class ImmediateTimerDispatcherProvider(ImmediateTimerStart start) : IDispatcherProvider
{
  public IDispatcher GetForCurrentThread() => ImmediateTimerDispatcher.For(start);
}

internal sealed class ImmediateTimerDispatcher : IDispatcher
{
  private static readonly ImmediateTimerDispatcher StopAfterTick = new(ImmediateTimerStart.StopAfterTick);
  private static readonly ImmediateTimerDispatcher RemainRunningWhenRepeating =
      new(ImmediateTimerStart.RemainRunningWhenRepeating);

  private readonly ImmediateTimerStart start;

  private ImmediateTimerDispatcher(ImmediateTimerStart start) => this.start = start;

  public static ImmediateTimerDispatcher For(ImmediateTimerStart start) => start switch
  {
    ImmediateTimerStart.StopAfterTick => StopAfterTick,
    ImmediateTimerStart.RemainRunningWhenRepeating => RemainRunningWhenRepeating,
    _ => throw new ArgumentOutOfRangeException(nameof(start), start, "Unknown timer start."),
  };

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

  public IDispatcherTimer CreateTimer() => new ImmediateDispatcherTimer(start);
}

internal sealed class ImmediateDispatcherTimer(ImmediateTimerStart start) : IDispatcherTimer
{
  public TimeSpan Interval { get; set; }

  public bool IsRepeating { get; set; }

  public bool IsRunning { get; private set; }

  public event EventHandler? Tick;

  public void Start()
  {
    IsRunning = true;
    Tick?.Invoke(this, EventArgs.Empty);
    if (start == ImmediateTimerStart.StopAfterTick || !IsRepeating) IsRunning = false;
  }

  public void Stop() => IsRunning = false;
}
