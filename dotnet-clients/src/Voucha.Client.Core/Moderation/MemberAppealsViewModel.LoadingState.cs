using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel
{
  private bool BeginLoad<T>(CursorState<T> state)
  {
    if (state.IsLoading) return false;
    state.IsLoading = true;
    state.HasError = false;
    OnLoadStateChanged();
    return true;
  }

  private void Complete<T>(
      CursorState<T> state,
      IReadOnlyList<T> incoming,
      PageInfo pageInfo,
      Func<T, string> id,
      bool replace)
  {
    if (replace) state.Items.Clear();
    var known = state.Items.Select(id).ToHashSet(StringComparer.Ordinal);
    state.Items.AddRange(incoming.Where(item => known.Add(id(item))));
    state.EndCursor = pageInfo.EndCursor;
    state.HasMore = pageInfo.HasNextPage || pageInfo.HasMore == true;
    state.IsLoading = false;
    state.HasError = false;
    state.HasSuccessfulLoad = true;
    state.CanRetry = false;
    state.RetryAfter = null;
    state.RetryReplace = false;
    OnLoadStateChanged();
  }

  private void CancelLoad<T>(CursorState<T> state)
  {
    state.IsLoading = false;
    OnLoadStateChanged();
  }

  private void FailLoad<T>(
      CursorState<T> state,
      string? after,
      bool replace)
  {
    state.IsLoading = false;
    state.HasError = true;
    state.CanRetry = true;
    state.RetryAfter = after;
    state.RetryReplace = replace;
    OnLoadStateChanged();
  }

  private void OnLoadStateChanged()
  {
    OnPropertyChanged(nameof(IsLoading));
    OnPropertyChanged(nameof(HasLoadError));
    OnPropertyChanged(nameof(WarningPagination));
    OnPropertyChanged(nameof(BanPagination));
    OnPropertyChanged(nameof(RemovedPostPagination));
  }

  private void OnTargetsChanged()
  {
    OnPropertyChanged(nameof(EligibleTargets));
    OnPropertyChanged(nameof(HasMoreWarnings));
    OnPropertyChanged(nameof(HasMoreBans));
    OnPropertyChanged(nameof(HasMoreRemovedPosts));
    OnLoadStateChanged();
  }
}
