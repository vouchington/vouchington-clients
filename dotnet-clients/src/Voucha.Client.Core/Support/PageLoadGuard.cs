using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Support;

public static class PageLoadGuard
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Page appearance loads report failures through view state and must not escape the lifecycle.")]
  public static async Task RunAsync(Func<Task> load)
  {
    ArgumentNullException.ThrowIfNull(load);
    try
    {
      await load().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
    }
  }
}
