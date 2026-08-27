using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class SupportThreadsViewModel
{
  public async Task LoadNextPageAsync(CancellationToken cancellationToken = default)
  {
    if (inFlight || !HasMore) return;
    var currentRequest = BeginLoad();
    try
    {
      var page = await chatService.FetchSupportThreadsAsync(
          cursor,
          25,
          cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return;
      foreach (var row in page.Results.Select(MapThread))
      {
        UpsertThread(row, moveToFront: false);
      }
      cursor = page.PageInfo.EndCursor;
      HasMore = page.PageInfo.HasNextPage || page.PageInfo.HasMore == true;
      OnPropertyChanged(nameof(Threads));
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        State = threads.Count == 0 ? LoadState.Idle : LoadState.Loaded;
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
    finally
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        inFlight = false;
      }
    }
  }

  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    inFlight = true;
    ErrorMessage = null;
    State = LoadState.Loading;
    return currentRequest;
  }
}
