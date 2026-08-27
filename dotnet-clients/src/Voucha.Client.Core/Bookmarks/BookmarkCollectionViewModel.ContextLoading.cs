using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Bookmarks;

public sealed partial class BookmarkCollectionViewModel
{
  public void SetContext(BookmarkCollectionRouteContext next)
  {
    ArgumentNullException.ThrowIfNull(next);
    lock (navigationGate)
    {
      contextGeneration++;
      pendingDestinations.Clear();
      resolvedDestinations.Clear();
      rows = [];
    }
    loadingContextGeneration = null;
    ResetPagination();
    IsLoading = false;
    Context = next;
    Title = localization.Resolve(next.TitleText);
    pendingActions.Clear();
    OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(HasRows));
    MutationErrorMessage = null;
    NavigationErrorMessage = null;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native bookmark collection workflows surface API failures in view state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    var route = Context;
    var loadContextGeneration = contextGeneration;
    if (IsLoading && loadingContextGeneration == loadContextGeneration)
    {
      return;
    }

    if (route is null)
    {
      Rows = [];
      return;
    }

    var userId = sessionStore.Current.Identity?.Id;
    if (!IsCurrentContext(route, loadContextGeneration))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(userId))
    {
      SetLocalizedError(UiText.Localized(UiMessageKey.NativeDotnetBookmarksSignInToView));
      Rows = [];
      return;
    }

    loadingContextGeneration = loadContextGeneration;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var result = await LoadRowsAsync(userId, route, cancellationToken).ConfigureAwait(true);
      if (IsCurrentContext(route, loadContextGeneration))
      {
        Rows = result.Rows;
        ApplyInitialPageInfo(result.PageInfo);
      }
    }
    catch (Exception ex)
    {
      if (IsCurrentContext(route, loadContextGeneration))
      {
        ErrorMessage = ex.Message;
        Rows = [];
        ApplyInitialPageInfo(null);
      }
    }
    finally
    {
      if (IsCurrentContext(route, loadContextGeneration) && loadingContextGeneration == loadContextGeneration)
      {
        loadingContextGeneration = null;
        IsLoading = false;
      }
    }
  }

  private Task<BookmarkCollectionLoadResult> LoadRowsAsync(
      string userId,
      BookmarkCollectionRouteContext route,
      CancellationToken cancellationToken) =>
      LoadCollectionRowsAsync(userId, route, cancellationToken);

  private bool IsCurrentContext(BookmarkCollectionRouteContext route, int generation) =>
      ReferenceEquals(Context, route) && contextGeneration == generation;
}
