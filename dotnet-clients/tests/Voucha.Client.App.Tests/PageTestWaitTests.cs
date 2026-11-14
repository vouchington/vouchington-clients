using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class PageTestWaitTests
{
  [Fact]
  public async Task ReturnsOnceTheConditionBecomesTrue()
  {
    var ready = false;
    var waiting = PageTestWait.WaitUntilAsync(() => ready);

    await Task.Delay(25, TestContext.Current.CancellationToken);

    Assert.False(waiting.IsCompleted);
    ready = true;
    await waiting;
  }

  [Fact]
  public async Task FailsWhenTheConditionStaysFalse()
  {
    var started = DateTime.UtcNow;

    var thrown = await Record.ExceptionAsync(() => PageTestWait.WaitUntilAsync(() => false));

    Assert.NotNull(thrown);
    Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(500));
  }
}
