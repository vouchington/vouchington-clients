using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PaymentCards;

namespace Voucha.Client.App.Pages;

public partial class PaymentCardsPage : ContentPage
{
  private readonly PaymentCardsViewModel viewModel;
  private readonly ViewportPaginationTrigger<HybridPaginationControl> paginationVisibility = new();
  private bool applyingParentSelection;

  public PaymentCardsPage(PaymentCardsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    SyncParentSelection();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The view model owns lifecycle error state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await LoadForAppearanceAsync();
      TryLoadVisiblePagination(PaymentCardsScroll, PaymentCardsScroll.ScrollY);
    }
    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
  }

  internal Task LoadForAppearanceAsync(CancellationToken cancellationToken = default) =>
      viewModel.EnsureLoadedAsync(cancellationToken);

  private async void OnSearchClicked(object? sender, EventArgs e) =>
      await viewModel.SearchTopicsAsync(viewModel.TopicSearchQuery);

  private async void OnAddTopicClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PaymentCardTopicOption option })
      await viewModel.CreateAsync(option.Value);
  }

  private void OnEditClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: PaymentCardRow row }) return;
    viewModel.BeginEdit(row);
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
  {
    if (eventArgs.PropertyName is nameof(PaymentCardsViewModel.Draft) or nameof(PaymentCardsViewModel.ParentCandidates))
      SyncParentSelection();
    if (eventArgs.PropertyName == nameof(PaymentCardsViewModel.HasDraft))
    {
      paginationVisibility.Rearm(ParentCardsPagination);
      TryLoadVisiblePagination(PaymentCardsScroll, PaymentCardsScroll.ScrollY);
    }
  }

  private void SyncParentSelection()
  {
    var selection = ParentPicker.ItemsSource?.Cast<PaymentCardOption>()
        .FirstOrDefault(option => option.Id == viewModel.Draft?.AuthorizedUserOfId);
    if (ReferenceEquals(ParentPicker.SelectedItem, selection)) return;
    applyingParentSelection = true;
    ParentPicker.SelectedItem = selection;
    applyingParentSelection = false;
  }

  private void OnParentSelected(object? sender, EventArgs e)
  {
    if (applyingParentSelection) return;
    if (viewModel.Draft is { } draft && ParentPicker.SelectedItem is PaymentCardOption option)
      draft.AuthorizedUserOfId = option.Id;
  }

  private async void OnSaveClicked(object? sender, EventArgs e) => await viewModel.SaveAsync();
  private void OnCancelClicked(object? sender, EventArgs e) => viewModel.CancelEdit();
  private async void OnRetryClicked(object? sender, EventArgs e) => await viewModel.RetryAsync();

  private async void OnLoadMoreRequested(object? sender, EventArgs eventArgs)
  {
    await viewModel.LoadMoreAsync();
    if (sender is HybridPaginationControl control) paginationVisibility.Rearm(control);
    TryLoadVisiblePagination(PaymentCardsScroll, PaymentCardsScroll.ScrollY);
  }

  private void OnPaymentCardsScrolled(object? sender, ScrolledEventArgs eventArgs)
  {
    if (sender is ScrollView scroll) TryLoadVisiblePagination(scroll, eventArgs.ScrollY);
  }

  private void OnPaymentCardsViewportChanged(object? sender, EventArgs eventArgs) =>
      TryLoadVisiblePagination(PaymentCardsScroll, PaymentCardsScroll.ScrollY);

  private void TryLoadVisiblePagination(ScrollView scroll, double scrollY)
  {
    if (scroll.Height <= 0 || scroll.Content is not VisualElement content) return;
    var candidates = scroll.GetVisualTreeDescendants()
        .OfType<HybridPaginationControl>()
        .Where(control => control.HasMore && IsEffectivelyVisible(control, content))
        .Select(control => (
            Control: control,
            Top: VerticalOffset(control, content),
            Height: control.Height > 0 ? control.Height : control.DesiredSize.Height))
        .Where(candidate => double.IsFinite(candidate.Top) && candidate.Height > 0)
        .ToArray();
    foreach (var control in paginationVisibility.EnteredViewport(candidates, scrollY, scroll.Height))
      control.TryLoadAutomatically();
  }

  internal static bool IsEffectivelyVisible(VisualElement control, VisualElement content)
  {
    Element? current = control;
    while (current is not null)
    {
      if (current is VisualElement visual && !visual.IsVisible) return false;
      if (ReferenceEquals(current, content)) return true;
      current = current.Parent;
    }
    return false;
  }

  private static double VerticalOffset(VisualElement control, VisualElement content)
  {
    var offset = 0d;
    Element? current = control;
    while (current is VisualElement visual && !ReferenceEquals(current, content))
    {
      offset += visual.Y;
      current = visual.Parent;
    }
    return ReferenceEquals(current, content) ? offset : double.PositiveInfinity;
  }

  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: PaymentCardRow row }) return;
    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetPaymentCardsDeleteConfirmationTitle),
        row.LocalizedName,
        UiCopy.Localize(UiMessageKey.NativeDotnetPaymentCardsDelete),
        UiCopy.Localize(UiMessageKey.CommonCancel));
    if (confirmed) await viewModel.DeleteAsync(row);
  }
}
