using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Lists;

public sealed partial class ListsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list workflows surface API failures in view state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      await LoadListsPageAsync(replace: true, cancellationToken).ConfigureAwait(true);
      var fallbackList = Lists.Count > 0 ? Lists[0] : null;
      SelectedList = SelectedList is { } selected
          ? Lists.FirstOrDefault(list => list.Id == selected.Id) ?? fallbackList
          : fallbackList;
      if (SelectedList is not null)
      {
        await LoadSelectedItemsAsync(cancellationToken).ConfigureAwait(true);
      }
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      Lists = [];
      Items = [];
      SelectedList = null;
    }
    finally
    {
      IsLoading = false;
    }
  }

  public void OnUiLocaleChanged()
  {
    Lists = Lists.Select(row => row.WithLocalization(localization)).ToArray();
    Items = Items.Select(row => row.WithLocalization(localization)).ToArray();
    SynchronizePaginationItems();
  }

  public void Dispose() => localeSubscription?.Dispose();
}
