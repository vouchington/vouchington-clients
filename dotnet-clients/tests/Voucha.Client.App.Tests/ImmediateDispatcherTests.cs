using Microsoft.Maui.Dispatching;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ImmediateDispatcherTests
{
  [Fact]
  public void DispatchRunsTheActionBeforeReturning()
  {
    var dispatcher = new ImmediateDispatcherProvider().GetForCurrentThread();
    var dispatchCalls = 0;
    var delayedCalls = 0;

    var dispatched = dispatcher.Dispatch(() => dispatchCalls++);
    Assert.Equal(1, dispatchCalls);
    var delayed = dispatcher.DispatchDelayed(TimeSpan.FromHours(1), () => delayedCalls++);
    Assert.Equal(1, delayedCalls);

    Assert.False(dispatcher.IsDispatchRequired);
    Assert.True(dispatched);
    Assert.True(delayed);
  }

  [Fact]
  public void CreateTimerIsUnsupported()
  {
    var dispatcher = new ImmediateDispatcherProvider().GetForCurrentThread();

    Assert.Throws<NotSupportedException>(() => dispatcher.CreateTimer());
  }

  [Fact]
  public void TimerDispatcherRunsActionsBeforeReturning()
  {
    var dispatcher = new ImmediateTimerDispatcherProvider(ImmediateTimerStart.StopAfterTick)
        .GetForCurrentThread();
    var calls = 0;

    Assert.True(dispatcher.Dispatch(() => calls++));
    Assert.True(dispatcher.DispatchDelayed(TimeSpan.FromHours(1), () => calls++));

    Assert.Equal(2, calls);
    Assert.False(dispatcher.IsDispatchRequired);
  }

  [Fact]
  public void StopAfterTickTimerClearsRunningAfterOneTick()
  {
    var timer = Timer(ImmediateTimerStart.StopAfterTick, repeating: true);
    var ticks = 0;
    timer.Tick += (_, _) => ticks++;

    timer.Start();

    Assert.Equal(1, ticks);
    Assert.False(timer.IsRunning);
    timer.Stop();
    Assert.False(timer.IsRunning);
  }

  [Fact]
  public void RepeatingTimerStaysRunningUntilStopped()
  {
    var repeating = Timer(ImmediateTimerStart.RemainRunningWhenRepeating, repeating: true);
    var once = Timer(ImmediateTimerStart.RemainRunningWhenRepeating, repeating: false);
    var ticks = 0;
    repeating.Tick += (_, _) => ticks++;
    once.Tick += (_, _) => ticks++;

    repeating.Start();
    once.Start();

    Assert.Equal(2, ticks);
    Assert.True(repeating.IsRunning);
    Assert.False(once.IsRunning);
    repeating.Stop();
    Assert.False(repeating.IsRunning);
  }

  private static IDispatcherTimer Timer(ImmediateTimerStart start, bool repeating)
  {
    var timer = new ImmediateTimerDispatcherProvider(start).GetForCurrentThread().CreateTimer();
    timer.IsRepeating = repeating;
    return timer;
  }
}
