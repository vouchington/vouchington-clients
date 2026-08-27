namespace Voucha.Client.App.Pages;

public static class StaffSupportPageOperation
{
  public static async Task RunAsync(
      Func<Task> action,
      Action<Exception> reportUnexpectedError,
      Action refresh)
  {
    ArgumentNullException.ThrowIfNull(action);
    ArgumentNullException.ThrowIfNull(reportUnexpectedError);
    ArgumentNullException.ThrowIfNull(refresh);
    try { await action().ConfigureAwait(true); }
    catch (Exception exception) { reportUnexpectedError(exception); }
    finally { refresh(); }
  }
}
