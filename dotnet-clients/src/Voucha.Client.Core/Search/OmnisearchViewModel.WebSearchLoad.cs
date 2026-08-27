using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native web-search surfaces API failures in view state before MAUI async handlers observe them.")]
  private async Task RunWebSearchLoadAsync(
      OmnisearchWebSearchSurface surface,
      Func<CancellationToken, Action<Action>, Task> load,
      CancellationToken cancellationToken)
  {
    var previousCancellation = Interlocked.Exchange(ref activeSearchCancellation, null);
    if (previousCancellation is not null)
    {
      await previousCancellation.CancelAsync().ConfigureAwait(true);
      previousCancellation.Dispose();
    }

    var requestId = ++searchRequestId;
    var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    Interlocked.Exchange(ref activeSearchCancellation, requestCancellation);
    ActiveSurface = surface;
    OnPropertyChanged(nameof(ActiveSurface));
    selectedHostnameId = null;
    selectedUrlId = null;
    selectedUrlCanTriggerCrawl = false;
    ResetReportState();
    UpdateSelectedActionProperties();
    IsLoading = true;
    ErrorMessage = null;

    try
    {
      await load(
          requestCancellation.Token,
          mutation =>
          {
            if (requestId == searchRequestId && ReferenceEquals(activeSearchCancellation, requestCancellation))
            {
              mutation();
            }
          }).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      if (requestId != searchRequestId || !ReferenceEquals(activeSearchCancellation, requestCancellation))
      {
        return;
      }

      Groups = [];
    }
    catch (Exception ex)
    {
      if (requestId != searchRequestId || !ReferenceEquals(activeSearchCancellation, requestCancellation))
      {
        return;
      }

      Groups = [];
      ErrorMessage = ex.Message;
    }
    finally
    {
      if (requestId == searchRequestId && ReferenceEquals(activeSearchCancellation, requestCancellation))
      {
        IsLoading = false;
        UpdateSelectedActionProperties();
      }

      if (ReferenceEquals(Interlocked.CompareExchange(ref activeSearchCancellation, null, requestCancellation), requestCancellation))
      {
        requestCancellation.Dispose();
      }

    }
  }
}
