using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Bookmarks;

public sealed partial class BookmarkCollectionViewModel
{
  public bool IsActionPending(BookmarkCollectionRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    return pendingActions.Contains(Identity(row));
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Bookmark failures are presented inline and rolled back.")]
  public async Task RemoveAsync(BookmarkCollectionRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    var identity = Identity(row);
    if (row.InverseAction is null || bookmarkService is null || !pendingActions.Add(identity)) return;

    var generation = contextGeneration;
    MutationErrorMessage = null;
    Rows = Rows.Where(candidate => !SameEntity(candidate, row)).ToArray();
    try
    {
      await bookmarkService.SetAsync(
          row.InverseAction.EntityType,
          row.Id,
          row.InverseAction.Predicate,
          false,
          cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      if (generation == contextGeneration)
      {
        Rows = Rows.Append(row with { IsActionPending = false }).OrderBy(candidate => candidate.Rank).ToArray();
        MutationErrorMessage = ex.Message;
      }
    }
    finally
    {
      pendingActions.Remove(identity);
    }
  }

  public async Task<string?> ResolveDestinationPathAsync(
      BookmarkCollectionRow row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    BookmarkOperationIdentity identity = default;
    Lazy<Task<string?>>? pending = null;
    string? immediatePath = null;
    var hasImmediateResult = false;
    var ownsNavigation = false;
    lock (navigationGate)
    {
      var currentRow = rows.FirstOrDefault(candidate => SameEntity(candidate, row));
      if (currentRow is null) return null;
      row = currentRow;
      navigationErrorMessage = null;
      if (row.DestinationPath is not null)
      {
        immediatePath = row.DestinationPath;
        hasImmediateResult = true;
      }
      else if (row.RootPostId is null)
      {
        hasImmediateResult = true;
      }
      else
      {
        identity = Identity(row);
        if (resolvedDestinations.TryGetValue(identity, out var cached))
        {
          immediatePath = cached;
          hasImmediateResult = true;
        }
        else
        {
          if (pendingDestinations.TryGetValue(identity, out var existing))
          {
            pending = existing;
          }
          else
          {
            ownsNavigation = true;
            pending = new(() => ResolveRootDestinationAsync(row, identity, cancellationToken));
            pendingDestinations[identity] = pending;
          }
        }
      }
    }
    OnPropertyChanged(nameof(NavigationErrorMessage));
    OnPropertyChanged(nameof(HasNavigationError));
    if (hasImmediateResult) return immediatePath;

    var task = pending!.Value;

    var path = await task.ConfigureAwait(true);
    return ownsNavigation ? path : null;
  }

  public void ReportNavigationFailure() =>
      NavigationErrorMessage = "We couldn't open this saved comment. Try again.";

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Saved comment navigation failures are presented inline.")]
  private async Task<string?> ResolveRootDestinationAsync(
      BookmarkCollectionRow row,
      BookmarkOperationIdentity identity,
      CancellationToken cancellationToken)
  {
    try
    {
      var response = await client.FetchPostAsync(row.RootPostId!, cancellationToken).ConfigureAwait(true);
      var path = $"{BookmarkCollectionRowFactory.DestinationForPost(response.Post)}/comment/{Uri.EscapeDataString(row.Id)}";
      return TryCommitResolvedDestination(row, identity, path) ? path : null;
    }
    catch (Exception)
    {
      TryReportNavigationFailure(identity);
      return null;
    }
    finally
    {
      lock (navigationGate) pendingDestinations.Remove(identity);
    }
  }

  private BookmarkOperationIdentity Identity(BookmarkCollectionRow row) =>
      new(contextGeneration, row.EntityType, row.Id);

  private bool TryCommitResolvedDestination(
      BookmarkCollectionRow row,
      BookmarkOperationIdentity identity,
      string path)
  {
    lock (navigationGate)
    {
      if (identity.Generation != contextGeneration) return false;
      resolvedDestinations[identity] = path;
      rows = rows.Select(candidate => SameEntity(candidate, row) ? candidate with { DestinationPath = path } : candidate).ToArray();
    }

    OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(HasRows));
    return true;
  }

  private void TryReportNavigationFailure(BookmarkOperationIdentity identity)
  {
    lock (navigationGate)
    {
      if (identity.Generation != contextGeneration) return;
      navigationErrorMessage = "We couldn't open this saved comment. Try again.";
    }

    OnPropertyChanged(nameof(NavigationErrorMessage));
    OnPropertyChanged(nameof(HasNavigationError));
  }

  private static bool SameEntity(BookmarkCollectionRow left, BookmarkCollectionRow right) =>
      left.EntityType == right.EntityType && left.Id == right.Id;
}
