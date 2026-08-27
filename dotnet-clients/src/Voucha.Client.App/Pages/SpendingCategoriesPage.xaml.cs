using System.ComponentModel;
using Voucha.Client.Core.SpendingCategories;

namespace Voucha.Client.App.Pages;

public partial class SpendingCategoriesPage : ContentPage, IDisposable
{
  private readonly SpendingCategoriesViewModel viewModel;
  private bool synchronizingFrequency;

  public SpendingCategoriesPage(SpendingCategoriesViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    RefreshFrequencyPickers();
    Unloaded += (_, _) => Dispose();
  }

  protected override async void OnAppearing() { base.OnAppearing(); await viewModel.EnsureLoadedAsync(); }
  private async void OnSearchClicked(object? sender, EventArgs e) => await viewModel.SearchAsync();
  private async void OnRetrySearchClicked(object? sender, EventArgs e) => await viewModel.SearchAsync();
  private async void OnCreateClicked(object? sender, EventArgs e) { if (sender is Button { CommandParameter: SpendingCategoryOptionRow row }) await viewModel.CreateAsync(row); }
  private void OnEditClicked(object? sender, EventArgs e) { if (sender is Button { CommandParameter: SpendingCategoryRow row }) { viewModel.BeginEdit(row); SyncEditFrequency(); } }
  private async void OnSaveClicked(object? sender, EventArgs e) => await viewModel.SaveAsync();
  private void OnCancelClicked(object? sender, EventArgs e) => viewModel.CancelEdit();
  private async void OnRetryClicked(object? sender, EventArgs e) => await viewModel.RetryAsync();
  private async void OnLoadMoreRequested(object? sender, EventArgs e) => await viewModel.LoadMoreAsync();
  private async void OnScrolled(object? sender, ScrolledEventArgs e)
  {
    if (sender is ScrollView scroll && viewModel.HasNextPage && !viewModel.HasContinuationError && !viewModel.IsLoading && !viewModel.IsLoadingMore && e.ScrollY + scroll.Height >= scroll.ContentSize.Height - 80)
      await viewModel.LoadMoreAsync();
  }
  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: SpendingCategoryRow row }) return;
    var confirmed = await DisplayAlertAsync(viewModel.DeleteTitle, row.LocalizedName, viewModel.DeleteConfirmText, viewModel.DeleteCancelText);
    if (confirmed) await viewModel.DeleteAsync(row);
  }
  private void OnCreateFrequencyChanged(object? sender, EventArgs e)
  {
    if (!synchronizingFrequency) viewModel.CreateDraft.Frequency = CreateFrequencyPicker.SelectedIndex == 1 ? "annually" : "monthly";
  }
  private void OnEditFrequencyChanged(object? sender, EventArgs e)
  {
    if (!synchronizingFrequency && viewModel.EditDraft is { } draft) draft.Frequency = EditFrequencyPicker.SelectedIndex == 1 ? "annually" : "monthly";
  }
  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(SpendingCategoriesViewModel.MonthlyFrequencyText) or nameof(SpendingCategoriesViewModel.AnnuallyFrequencyText) or nameof(SpendingCategoriesViewModel.EditDraft) or nameof(SpendingCategoriesViewModel.CreateDraft))
      RefreshFrequencyPickers();
  }
  private void RefreshFrequencyPickers()
  {
    synchronizingFrequency = true;
    CreateFrequencyPicker.ItemsSource = new[] { viewModel.MonthlyFrequencyText, viewModel.AnnuallyFrequencyText };
    EditFrequencyPicker.ItemsSource = new[] { viewModel.MonthlyFrequencyText, viewModel.AnnuallyFrequencyText };
    CreateFrequencyPicker.SelectedIndex = viewModel.CreateDraft.Frequency == "annually" ? 1 : 0;
    SyncEditFrequency();
    synchronizingFrequency = false;
  }
  private void SyncEditFrequency()
  {
    synchronizingFrequency = true;
    EditFrequencyPicker.SelectedIndex = viewModel.EditDraft?.Frequency == "annually" ? 1 : 0;
    synchronizingFrequency = false;
  }
  public void Dispose() { viewModel.PropertyChanged -= OnViewModelPropertyChanged; viewModel.Dispose(); }
}
