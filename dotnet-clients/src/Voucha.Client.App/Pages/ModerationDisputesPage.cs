using System.ComponentModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationDisputesPage : ContentPage, IDisposable
{
  private readonly ModerationDisputesViewModel viewModel;
  private readonly Picker statusPicker = UiCopy.Bind(
      new Picker { AutomationId = "review-disputes-status" },
      Picker.TitleProperty,
      UiMessageKey.NativeSwiftReviewDisputesStatus);
  private readonly CollectionView disputesView = new()
  {
    AutomationId = "review-disputes-list",
    ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
  };
  private readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  private readonly ActivityIndicator loadingIndicator = new();
  private readonly HybridPaginationControl pagination = new()
  {
    PaginationId = "review-disputes",
  };
  private readonly Label emptyLabel = new()
  {
    AutomationId = "review-disputes-empty",
    HorizontalTextAlignment = TextAlignment.Center,
  };
  public ModerationDisputesPage(ModerationDisputesViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetModerationReviewDisputes.Value);
    if (!viewModel.IsSignedIn)
    {
      Content = AccessLabel(
          UiMessageKey.NativeSwiftReviewDisputesSignInMessage,
          "review-disputes-sign-in");
      return;
    }
    if (!viewModel.CanAccess)
    {
      Content = AccessLabel(
          UiMessageKey.NativeSwiftReviewDisputesStaffAccessMessage,
          "review-disputes-role-denied");
      return;
    }

    ConfigureStatusPicker();
    statusPicker.SelectedIndexChanged += StatusChangedAsync;
    disputesView.ItemTemplate = new DataTemplate(() =>
        new ModerationDisputeCardView(viewModel, CurrentMutationToken));
    disputesView.SetBinding(
        ItemsView.ItemsSourceProperty,
        nameof(ModerationDisputesViewModel.Disputes));
    pagination.LoadNextPageRequested += LoadNextPageAsync;
    pagination.SetBinding(
        HybridPaginationControl.HasMoreProperty,
        nameof(ModerationDisputesViewModel.HasMore));
    pagination.SetBinding(
        HybridPaginationControl.IsLoadingProperty,
        nameof(ModerationDisputesViewModel.IsLoading));
    pagination.SetBinding(
        HybridPaginationControl.HasErrorProperty,
        nameof(ModerationDisputesViewModel.HasError));
    disputesView.Footer = pagination;
    disputesView.RemainingItemsThreshold = 2;
    disputesView.RemainingItemsThresholdReached += RemainingItemsThresholdReached;
    errorLabel.SetBinding(
        Label.TextProperty,
        nameof(ModerationDisputesViewModel.ErrorMessage));
    loadingIndicator.SetBinding(
        ActivityIndicator.IsRunningProperty,
        nameof(ModerationDisputesViewModel.IsLoading));
    loadingIndicator.SetBinding(
        IsVisibleProperty,
        nameof(ModerationDisputesViewModel.IsLoading));
    emptyLabel.SetBinding(
        IsVisibleProperty,
        nameof(ModerationDisputesViewModel.IsEmpty));
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    Content = CreateStaffContent();
  }

  private View CreateStaffContent()
  {
    var refresh = UiCopy.Bind(
        new Button { AutomationId = "review-disputes-refresh" },
        Button.TextProperty,
        UiMessageKey.NativeDotnetCommonRefresh);
    refresh.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var grid = new Grid
    {
      Padding = 16,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Star),
      },
    };
    grid.Add(statusPicker);
    grid.Add(refresh, 0, 1);
    grid.Add(errorLabel, 0, 2);
    grid.Add(disputesView, 0, 3);
    grid.Add(emptyLabel, 0, 3);
    grid.Add(loadingIndicator, 0, 3);
    return grid;
  }

  private async void StatusChangedAsync(object? sender, EventArgs args)
  {
    var status = statusPicker.SelectedIndex switch
    {
      0 => ModerationDisputeStatus.Pending,
      1 => ModerationDisputeStatus.Resolved,
      2 => ModerationDisputeStatus.Dismissed,
      _ => (ModerationDisputeStatus?)null,
    };
    if (status is not { } next) return;
    await RunLatestLoadAsync(token =>
        viewModel.SelectStatusAsync(next, token)).ConfigureAwait(true);
  }

  private async void LoadNextPageAsync(object? sender, EventArgs args) =>
      await RunLatestLoadAsync(viewModel.LoadMoreAsync).ConfigureAwait(true);

  private void RemainingItemsThresholdReached(object? sender, EventArgs args) =>
      pagination.TryLoadAutomatically();

  private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
  {
    if (args.PropertyName is nameof(ModerationDisputesViewModel.SelectedStatus)
        or nameof(ModerationDisputesViewModel.State)
        or nameof(ModerationDisputesViewModel.Disputes))
    {
      ConfigureStatusPicker();
      ConfigureEmptyLabel();
    }
  }

  private void ConfigureStatusPicker()
  {
    statusPicker.ItemsSource = new[]
    {
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsPending),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsResolved),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsDismissed),
    };
    statusPicker.SelectedIndex = viewModel.SelectedStatus switch
    {
      ModerationDisputeStatus.Pending => 0,
      ModerationDisputeStatus.Resolved => 1,
      ModerationDisputeStatus.Dismissed => 2,
      _ => -1,
    };
  }

  private void ConfigureEmptyLabel() =>
      emptyLabel.Text = UiCopy.Localize(viewModel.SelectedStatus switch
      {
        ModerationDisputeStatus.Pending => UiMessageKey.NativeSwiftReviewDisputesNoPending,
        ModerationDisputeStatus.Resolved => UiMessageKey.NativeSwiftReviewDisputesNoResolved,
        ModerationDisputeStatus.Dismissed => UiMessageKey.NativeSwiftReviewDisputesNoDismissed,
        _ => UiMessageKey.NativeSwiftReviewDisputesEmptyMessage,
      });

  private static Label AccessLabel(UiMessageKey key, string id) =>
      UiCopy.Bind(
          new Label { AutomationId = id, Margin = 16 },
          Label.TextProperty,
          key);
}
