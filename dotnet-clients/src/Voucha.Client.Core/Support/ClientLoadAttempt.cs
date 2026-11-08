using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Support;

public static class ClientLoadAttempt
{
  public static async Task RunAsync(
      Func<Task> work,
      Action onCanceled,
      Action<string> onError)
  {
    ArgumentNullException.ThrowIfNull(work);
    ArgumentNullException.ThrowIfNull(onCanceled);
    ArgumentNullException.ThrowIfNull(onError);
    try
    {
      await work().ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      onCanceled();
    }
    catch (VouchaApiException ex)
    {
      onError(ex.Message);
    }
    catch (HttpRequestException ex)
    {
      onError(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      onError(ex.Message);
    }
  }
}
