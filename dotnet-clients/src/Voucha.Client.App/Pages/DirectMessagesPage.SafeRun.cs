using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.App.Pages;

public partial class DirectMessagesPage
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow view-model failures to escape to the dispatcher.")]
  private static async Task<bool> RunSafelyAsync(Func<Task> action)
  {
    try
    {
      await action().ConfigureAwait(true);
      return true;
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
      return false;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow view-model failures to escape to the dispatcher.")]
  private static async Task<bool> RunSafelyAsync(Func<Task<bool>> action)
  {
    try
    {
      return await action().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
      return false;
    }
  }
}
