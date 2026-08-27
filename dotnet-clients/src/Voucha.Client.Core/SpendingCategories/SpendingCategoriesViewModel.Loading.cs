using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Presentation state owns list failures.")]
  public async Task LoadAsync(CancellationToken token = default)
  {
    if (IsLoading) return;
    var generation = unchecked(++loadGeneration); var readId = BeginRead();
    IsLoading = true; HasContinuationError = false; SetError(null);
    try
    {
      var page = await service.FetchAsync(cancellationToken: token).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      var response = page.Results.ToDictionary(value => value.Id, StringComparer.Ordinal);
      var display = new Dictionary<string, SpendingCategory>(response, StringComparer.Ordinal);
      Reconcile(display, response, readId, generation, replacesList: true, reachesEnd: !page.PageInfo.HasNextPage); Replace(display.Values);
      pageInfo = page.PageInfo; loaded = true; Notify(nameof(HasNextPage));
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception error) { if (generation == loadGeneration) Fail(error); }
    finally { CompleteRead(readId); if (generation == loadGeneration) IsLoading = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve rows.")]
  public async Task LoadMoreAsync(CancellationToken token = default)
  {
    if (IsLoading || IsLoadingMore || HasContinuationError || !pageInfo.HasNextPage || pageInfo.EndCursor is null) return;
    var generation = loadGeneration; var readId = BeginRead();
    IsLoadingMore = true; HasContinuationError = false; SetError(null);
    try
    {
      var page = await service.FetchAsync(pageInfo.EndCursor, cancellationToken: token).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      var response = page.Results.ToDictionary(value => value.Id, StringComparer.Ordinal);
      var display = values.Concat(page.Results).GroupBy(value => value.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
      Reconcile(display, response, readId, generation, replacesList: false, reachesEnd: !page.PageInfo.HasNextPage); Replace(display.Values);
      pageInfo = page.PageInfo; Notify(nameof(HasNextPage));
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception error) { if (generation == loadGeneration) { HasContinuationError = true; Fail(error); } }
    finally { CompleteRead(readId); if (generation == loadGeneration) IsLoadingMore = false; }
  }
}
