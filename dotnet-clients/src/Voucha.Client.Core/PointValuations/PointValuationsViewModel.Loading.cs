using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.PointValuations;

public sealed partial class PointValuationsViewModel
{
  public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
      hasLoaded ? Task.CompletedTask : LoadAsync(cancellationToken);

  public Task RetryAsync(CancellationToken cancellationToken = default) =>
      HasContinuationError ? LoadMoreAsync(cancellationToken) : LoadAsync(cancellationToken);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Presentation state owns list failures.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    var generation = unchecked(++loadGeneration);
    var readId = BeginRead();
    IsLoading = true; HasContinuationError = false; SetError(null);
    try
    {
      var page = await service.FetchAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      var server = page.Results.ToDictionary(value => value.Id, StringComparer.Ordinal);
      ApplyLocalUpserts(server, readId);
      ApplyPendingDeletions(server, readId);
      Replace(server.Values);
      pageInfo = page.PageInfo; hasLoaded = true;
      OnPropertyChanged(nameof(HasNextPage)); OnPropertyChanged(nameof(ShowsEmptyState));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { if (generation == loadGeneration) Fail(error); }
    finally
    {
      CompleteRead(readId);
      if (generation == loadGeneration) IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve rows.")]
  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || IsLoadingMore || !pageInfo.HasNextPage || pageInfo.EndCursor is null) return;
    var generation = loadGeneration;
    var readId = BeginRead();
    IsLoadingMore = true; HasContinuationError = false; SetError(null);
    try
    {
      var page = await service.FetchAsync(pageInfo.EndCursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      var merged = valuations.Concat(page.Results)
          .GroupBy(value => value.Id, StringComparer.Ordinal)
          .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
      ApplyLocalUpserts(merged, readId);
      ApplyPendingDeletions(merged, readId);
      Replace(merged.Values); pageInfo = page.PageInfo;
      OnPropertyChanged(nameof(HasNextPage));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error)
    {
      if (generation == loadGeneration) { HasContinuationError = true; Fail(error); }
    }
    finally
    {
      CompleteRead(readId);
      if (generation == loadGeneration) IsLoadingMore = false;
    }
  }

  private int BeginRead()
  {
    var readId = unchecked(++nextReadId);
    inFlightReadIds.Add(readId);
    foreach (var deletion in pendingDeletions.Values)
      if (deletion.MutationPending) deletion.PendingReadIds.Add(readId);
    return readId;
  }

  private void ApplyLocalUpserts(Dictionary<string, PointValuation> values, int readId)
  {
    foreach (var local in localUpserts.Values)
      if (local.PendingReadIds.Contains(readId)) values[local.Value.Id] = local.Value;
  }

  private void CompleteRead(int readId)
  {
    inFlightReadIds.Remove(readId);
    foreach (var (id, local) in localUpserts.ToArray())
    {
      local.PendingReadIds.Remove(readId);
      if (local.PendingReadIds.Count == 0) localUpserts.Remove(id);
    }
    foreach (var (id, deletion) in pendingDeletions.ToArray())
    {
      deletion.PendingReadIds.Remove(readId);
      if (!deletion.MutationPending && deletion.PendingReadIds.Count == 0)
        pendingDeletions.Remove(id);
    }
  }

  private void ApplyPendingDeletions(Dictionary<string, PointValuation> values, int readId)
  {
    foreach (var (id, deletion) in pendingDeletions)
      if (deletion.PendingReadIds.Contains(readId)) values.Remove(id);
  }

  private sealed record PendingLocalUpsert(PointValuation Value, HashSet<int> PendingReadIds);
  private sealed record PendingLocalDeletion(HashSet<int> PendingReadIds)
  {
    public bool MutationPending { get; set; } = true;
  }
}
