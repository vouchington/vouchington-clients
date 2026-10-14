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
}
