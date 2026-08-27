using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow view-model or navigation failures to escape.")]
  private async Task RunProfileActionAsync(Func<Task> action)
  {
    try
    {
      await action().ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      viewModel.ReportUnexpectedProfileActionError(ex);
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
