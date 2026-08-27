using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel
{
  public Task LoadMoreAppealsAsync(
      ModerationAppealStatus status,
      CancellationToken cancellationToken = default)
  {
    var state = appealStates[status];
    if (state.HasError && state.CanRetry)
      return LoadAppealsAsync(
          status, state.RetryAfter, state.RetryReplace, cancellationToken);
    return state.HasMore && state.EndCursor is not null
        ? LoadAppealsAsync(status, state.EndCursor, replace: false, cancellationToken)
        : Task.CompletedTask;
  }

  public async Task LoadMoreWarningsAsync(CancellationToken cancellationToken = default)
  {
    if (!await ReconcilePendingAppealsAsync(cancellationToken).ConfigureAwait(true)) return;
    if (warnings.HasError && warnings.CanRetry)
      await LoadWarningsAsync(
          warnings.RetryAfter, warnings.RetryReplace, cancellationToken).ConfigureAwait(true);
    else if (warnings.HasMore && warnings.EndCursor is not null)
      await LoadWarningsAsync(warnings.EndCursor, replace: false, cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadMoreBansAsync(CancellationToken cancellationToken = default)
  {
    if (!await ReconcilePendingAppealsAsync(cancellationToken).ConfigureAwait(true)) return;
    if (bans.HasError && bans.CanRetry)
      await LoadBansAsync(
          bans.RetryAfter, bans.RetryReplace, cancellationToken).ConfigureAwait(true);
    else if (bans.HasMore && bans.EndCursor is not null)
      await LoadBansAsync(bans.EndCursor, replace: false, cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadMoreRemovedPostsAsync(CancellationToken cancellationToken = default)
  {
    if (!await ReconcilePendingAppealsAsync(cancellationToken).ConfigureAwait(true)) return;
    if (removals.HasError && removals.CanRetry)
      await LoadRemovedPostsAsync(
          removals.RetryAfter, removals.RetryReplace, cancellationToken).ConfigureAwait(true);
    else if (removals.HasMore && removals.EndCursor is not null)
      await LoadRemovedPostsAsync(removals.EndCursor, replace: false, cancellationToken).ConfigureAwait(true);
  }

  private async Task<bool> ReconcilePendingAppealsAsync(CancellationToken cancellationToken)
  {
    var state = appealStates[ModerationAppealStatus.Pending];
    if (state.IsLoading) return false;
    while ((state.HasError && state.CanRetry) || state.HasMore)
    {
      if (state.IsLoading) return false;
      var after = state.HasError ? state.RetryAfter : state.EndCursor;
      var replace = state.HasError && state.RetryReplace;
      if (after is null && !replace) return false;
      await LoadAppealsAsync(
          ModerationAppealStatus.Pending, after, replace, cancellationToken).ConfigureAwait(true);
      if (state.HasError) return false;
    }
    return state.HasSuccessfulLoad;
  }
}
