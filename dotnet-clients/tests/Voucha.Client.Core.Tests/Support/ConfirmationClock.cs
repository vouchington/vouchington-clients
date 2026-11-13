namespace Voucha.Client.Core.Tests.Support;

internal sealed class ConfirmationClock : TimeProvider
{
  private readonly List<ClockTimer> timers = [];
  private DateTimeOffset now = DateTimeOffset.UtcNow;
  public TaskCompletionSource PollDelayCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
  public override DateTimeOffset GetUtcNow() => now;
  public void Jump(TimeSpan value) => now += value;
  public void Advance(TimeSpan value)
  {
    Jump(value);
    foreach (var timer in timers.ToArray()) timer.FireIfDue();
  }
  public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
  {
    var timer = new ClockTimer(this, callback, state);
    timers.Add(timer);
    timer.Change(dueTime, period);
    if (dueTime == TimeSpan.FromSeconds(1)) PollDelayCreated.TrySetResult();
    return timer;
  }

  private sealed class ClockTimer(ConfirmationClock clock, TimerCallback callback, object? state) : ITimer
  {
    private DateTimeOffset due = DateTimeOffset.MaxValue;
    private bool disposed;
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
      if (disposed) return false;
      due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.GetUtcNow() + dueTime;
      return true;
    }
    public void FireIfDue()
    {
      if (disposed || due > clock.GetUtcNow()) return;
      due = DateTimeOffset.MaxValue;
      callback(state);
    }
    public void Dispose() => disposed = true;
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
  }
}
