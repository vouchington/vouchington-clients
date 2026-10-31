using Xunit;

namespace Voucha.Client.App.Tests;

internal static class PageTestWait
{
  public static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++)
      await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }
}
