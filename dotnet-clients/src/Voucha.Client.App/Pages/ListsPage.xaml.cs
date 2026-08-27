using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ListsPage : ContentPage
{
  private readonly ListsViewModel viewModel;

  public ListsPage(ListsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    BindingContext = new ListsPageBinding(viewModel);
    ListsPaginationControl.LoadNextPageRequested += OnLoadMoreListsRequested;
    ItemsPaginationControl.LoadNextPageRequested += OnLoadMoreItemsRequested;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync();
  }

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    await viewModel.CreateListAsync(ListNameEntry.Text ?? string.Empty);
    ListNameEntry.Text = string.Empty;
  }

  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    if (viewModel.SelectedList is null)
    {
      return;
    }

    if (!await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetDynamicDeleteList),
        UiCopy.Format(UiMessageKey.NativeDotnetResidualDeleteNamedList, ("name", viewModel.SelectedList.Name)),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDelete),
        UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true))
    {
      return;
    }

    await viewModel.DeleteSelectedListAsync();
  }

  private async void OnSaveClicked(object? sender, EventArgs e)
  {
    await viewModel.UpdateSelectedListAsync(
        viewModel.SelectedListName,
        viewModel.SelectedListDescription);
  }

  private async void OnImportClicked(object? sender, EventArgs e)
  {
    await viewModel.ImportCommunityAsync(CommunitySlugEntry.Text ?? string.Empty);
    CommunitySlugEntry.Text = string.Empty;
  }

  private async void OnAllClicked(object? sender, EventArgs e) =>
      await viewModel.SelectFilterAsync(ListItemFilter.All);

  private async void OnReadingClicked(object? sender, EventArgs e) =>
      await viewModel.SelectFilterAsync(ListItemFilter.Reading);

  private async void OnWatchClicked(object? sender, EventArgs e) =>
      await viewModel.SelectFilterAsync(ListItemFilter.Watch);

  private async void OnListenClicked(object? sender, EventArgs e) =>
      await viewModel.SelectFilterAsync(ListItemFilter.Listen);

  private async void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is ListSummaryRow list && !viewModel.IsLoading)
    {
      await viewModel.SelectListAsync(list);
    }
  }

  private void OnListsThresholdReached(object? sender, EventArgs e) =>
      ListsPaginationControl.TryLoadAutomatically();

  private void OnItemsThresholdReached(object? sender, EventArgs e) =>
      ItemsPaginationControl.TryLoadAutomatically();

  private async void OnLoadMoreListsRequested(object? sender, EventArgs e) =>
      await viewModel.LoadMoreListsAsync().ConfigureAwait(true);

  private async void OnLoadMoreItemsRequested(object? sender, EventArgs e) =>
      await viewModel.LoadMoreItemsAsync().ConfigureAwait(true);
}
