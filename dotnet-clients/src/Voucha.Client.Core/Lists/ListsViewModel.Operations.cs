using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Lists;

public sealed partial class ListsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list workflows surface API failures in view state.")]
  public async Task CreateListAsync(string name, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name)) return;
    ErrorMessage = null;
    try
    {
      var response = await client.CreateListAsync(
          new ListMutationBody(name.Trim(), Visibility: "private"),
          cancellationToken).ConfigureAwait(true);
      var created = ListSummaryRow.FromList(response.List, localization);
      Lists = [created, .. Lists.Where(list => list.Id != created.Id)];
      SynchronizeListPaginationItems();
      SelectedList = created;
      Items = [];
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list workflows surface API failures in view state.")]
  public async Task UpdateSelectedListAsync(
      string? name,
      string? description,
      CancellationToken cancellationToken = default)
  {
    ErrorMessage = null;
    if (SelectedList is null) return;
    var requestListId = SelectedList.Id;
    var trimmedName = name?.Trim();
    if (string.IsNullOrWhiteSpace(trimmedName)) return;
    try
    {
      var trimmedDescription = description?.Trim();
      var response = await client.UpdateListAsync(
          requestListId,
          new ListMutationBody(
              trimmedName,
              string.IsNullOrEmpty(trimmedDescription)
                  ? JsonNullableString.Null
                  : JsonNullableString.FromString(trimmedDescription)),
          cancellationToken).ConfigureAwait(true);
      var updated = ListSummaryRow.FromList(response.List, localization);
      var updatedLists = Lists
          .Select(list => list.Id == updated.Id ? updated : list)
          .ToArray();

      Lists = updatedLists;
      SynchronizeListPaginationItems();
      if (SelectedList?.Id == requestListId)
      {
        SelectedList = updated;
      }
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list workflows surface API failures in view state.")]
  public async Task DeleteSelectedListAsync(CancellationToken cancellationToken = default)
  {
    if (SelectedList is null) return;
    ErrorMessage = null;
    try
    {
      await client.DeleteListAsync(SelectedList.Id, cancellationToken).ConfigureAwait(true);
      SelectedList = null;
      Items = [];
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list workflows surface API failures in view state.")]
  public async Task ImportCommunityAsync(string communitySlug, CancellationToken cancellationToken = default)
  {
    if (SelectedList is null || string.IsNullOrWhiteSpace(communitySlug)) return;
    ErrorMessage = null;
    try
    {
      await client.ImportCommunityListAsync(SelectedList.Id, communitySlug.Trim(), cancellationToken).ConfigureAwait(true);
      await LoadSelectedItemsAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  private Task LoadSelectedItemsAsync(CancellationToken cancellationToken) =>
      LoadItemsPageAsync(replace: true, cancellationToken);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native list filter workflows surface API failures in view state before MAUI async event handlers observe them.")]
  private async Task LoadSelectedItemsWithErrorStateAsync(CancellationToken cancellationToken)
  {
    ErrorMessage = null;
    try
    {
      await LoadSelectedItemsAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      Items = [];
    }
    catch (Exception ex)
    {
      Items = [];
      ErrorMessage = ex.Message;
    }
  }

  private static string? MediaTypeForFilter(ListItemFilter filter) =>
      filter switch
      {
        ListItemFilter.Reading => "article",
        ListItemFilter.Watch => "video",
        ListItemFilter.Listen => "audio",
        _ => null,
      };
}
