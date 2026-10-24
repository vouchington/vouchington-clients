using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;

namespace Voucha.Client.App.Pages;

public abstract class IntegrityFlagQueuePageBase<TViewModel> : ContentPage
    where TViewModel : class, IIntegrityFlagQueueModel, INotifyPropertyChanged
{
  protected readonly TViewModel viewModel;
  protected readonly Picker statusPicker;
  protected readonly VerticalStackLayout queue = new() { Spacing = 12 };
  protected readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  protected readonly Button retryButton;
  protected readonly Button loadMoreButton;
  private CancellationTokenSource? pageCancellation;
  protected bool applyingStatus;
  protected readonly HashSet<string> confirmingPenaltyIds = new(StringComparer.Ordinal);

  protected IntegrityFlagQueuePageBase(
      TViewModel viewModel,
      UiMessageKey titleKey,
      string flagsPath,
      string penaltiesPath,
      string automationPrefix)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    SetDynamicResource(TitleProperty, titleKey.Value);
    BindingContext = viewModel;
    statusPicker = UiCopy.Bind(
        new Picker { AutomationId = $"{automationPrefix}-status" },
        Picker.TitleProperty, UiMessageKey.NativeSwiftIntegrityStatus);
    retryButton = UiCopy.Bind(
        new Button { AutomationId = $"{automationPrefix}-retry" },
        Button.TextProperty, UiMessageKey.NativeSwiftIntegrityRetry);
    loadMoreButton = UiCopy.Bind(
        new Button { AutomationId = $"{automationPrefix}-load-more" },
        Button.TextProperty, UiMessageKey.NativeSwiftIntegrityLoadMore);
    ConfigureStatusPicker();
    loadMoreButton.Clicked += async (_, _) =>
        await viewModel.LoadMoreAsync(CurrentToken()).ConfigureAwait(true);
    retryButton.Clicked += async (_, _) =>
        await viewModel.ReloadAsync(CurrentToken()).ConfigureAwait(true);
    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          UiCopy.Bind(new Label { FontSize = 24, FontAttributes = FontAttributes.Bold },
              Label.TextProperty, titleKey),
          IntegrityPageViews.SectionNavigation(flagsPath, penaltiesPath, automationPrefix),
          statusPicker,
          errorLabel,
          retryButton,
          queue,
          loadMoreButton,
        },
      },
    };
  }

  public Task ReloadAsync() => viewModel.ReloadAsync(CurrentToken());

  protected void InitializeRender(Action render)
  {
    viewModel.PropertyChanged += (_, _) => render();
    render();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync(CurrentToken()).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    pageCancellation?.Cancel();
    pageCancellation?.Dispose();
    pageCancellation = null;
    base.OnDisappearing();
  }

  protected async Task RunActionAsync(Func<CancellationToken, Task> action)
  {
    try { await action(CurrentToken()).ConfigureAwait(true); }
    catch (OperationCanceledException) { }
  }

  protected static string StatusLabel(IntegrityFlagStatus status) => status switch
  {
    IntegrityFlagStatus.Pending => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPending),
    IntegrityFlagStatus.Resolved => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolved),
    _ => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityAll),
  };

  private void ConfigureStatusPicker()
  {
    statusPicker.ItemsSource = viewModel.AvailableStatuses.Select(StatusLabel).ToArray();
    statusPicker.SelectedIndex = Array.IndexOf(
        viewModel.AvailableStatuses.ToArray(),
        viewModel.Status);
    statusPicker.SelectedIndexChanged += async (_, _) =>
    {
      if (applyingStatus || statusPicker.SelectedIndex < 0) return;
      await viewModel.SelectStatusAsync(
          viewModel.AvailableStatuses[statusPicker.SelectedIndex],
          CurrentToken()).ConfigureAwait(true);
    };
  }

  private CancellationToken CurrentToken()
  {
    pageCancellation ??= new CancellationTokenSource();
    return pageCancellation.Token;
  }
}
