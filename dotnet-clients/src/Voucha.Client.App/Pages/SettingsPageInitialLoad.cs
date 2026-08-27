namespace Voucha.Client.App.Pages;

internal static class SettingsPageInitialLoad
{
  public static async Task RunAsync(Func<Task> focusedLoad, Func<Task> broadLoad)
  {
    ArgumentNullException.ThrowIfNull(focusedLoad);
    ArgumentNullException.ThrowIfNull(broadLoad);

    var focusedLoadTask = Start(focusedLoad);
    var broadLoadTask = Start(broadLoad);
    await Task.WhenAll(focusedLoadTask, broadLoadTask).ConfigureAwait(true);
  }

  private static Task Start(Func<Task> load)
  {
    try
    {
      return load() ?? Task.FromException(new InvalidOperationException("A page load returned no task."));
    }
    catch (Exception exception)
    {
      return Task.FromException(exception);
    }
  }
}
