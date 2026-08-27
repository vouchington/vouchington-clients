using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class AuthViewModel
{
  private async Task RunAsync(Func<Task> operation)
  {
    if (IsLoading) return;
    IsLoading = true;
    StatusMessage = null;
    try
    {
      await operation().ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      StatusMessage = null;
    }
    catch (VouchaApiException ex)
    {
      StatusMessage = ex.Message;
    }
    catch (HttpRequestException ex)
    {
      StatusMessage = ex.Message;
    }
    catch (InvalidOperationException ex)
    {
      StatusMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  private void NotifySessionStateChanged()
  {
    OnPropertyChanged(nameof(IsAuthenticated));
    OnPropertyChanged(nameof(SessionLabel));
  }
}
