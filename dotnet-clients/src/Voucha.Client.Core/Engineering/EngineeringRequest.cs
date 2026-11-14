using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

internal static class EngineeringRequest
{
  public static bool IsSurfaced(Exception exception) =>
      exception is VouchaApiException
          or HttpRequestException
          or InvalidOperationException
          or JsonException
          or IOException;

  public static async Task RunAsync(
      Func<bool> isBusy,
      Action begin,
      Action<string?> setError,
      Action end,
      Func<Task> action)
  {
    ArgumentNullException.ThrowIfNull(isBusy);
    ArgumentNullException.ThrowIfNull(begin);
    ArgumentNullException.ThrowIfNull(setError);
    ArgumentNullException.ThrowIfNull(end);
    ArgumentNullException.ThrowIfNull(action);
    if (isBusy()) return;

    begin();
    setError(null);
    try
    {
      await action().ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      setError(null);
    }
    catch (Exception exception) when (IsSurfaced(exception))
    {
      setError(exception.Message);
    }
    finally
    {
      end();
    }
  }
}
