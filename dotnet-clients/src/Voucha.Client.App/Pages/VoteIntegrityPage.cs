using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class VoteIntegrityPage : ContentPage
{
  private readonly VoteIntegrityViewModel viewModel;
  private readonly Picker statusPicker = UiCopy.Bind(
      new Picker { AutomationId = "vote-integrity-status" },
      Picker.TitleProperty, UiMessageKey.NativeSwiftIntegrityStatus);
  private readonly VerticalStackLayout queue = new() { Spacing = 12 };
  private readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  private readonly Button retryButton = UiCopy.Bind(
      new Button { AutomationId = "vote-integrity-retry" },
      Button.TextProperty, UiMessageKey.NativeSwiftIntegrityRetry);
  private readonly Button loadMoreButton = UiCopy.Bind(
      new Button { AutomationId = "vote-integrity-load-more" },
      Button.TextProperty, UiMessageKey.NativeSwiftIntegrityLoadMore);
  private CancellationTokenSource? pageCancellation;
  private bool applyingStatus;
  private readonly HashSet<string> confirmingPenaltyIds = new(StringComparer.Ordinal);

  public VoteIntegrityPage(VoteIntegrityViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetModerationVoteIntegrity.Value);
    BindingContext = viewModel;
    ConfigureStatusPicker();
    viewModel.PropertyChanged += ViewModelPropertyChanged;
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
              Label.TextProperty, UiMessageKey.NativeDotnetModerationVoteIntegrity),
          IntegrityPageViews.SectionNavigation(
              "/vote-integrity/flags", "/vote-integrity/penalties",
              "vote-integrity"),
          statusPicker,
          errorLabel,
          retryButton,
          queue,
          loadMoreButton,
        },
      },
    };
    Render();
  }

  public Task ReloadAsync() => viewModel.ReloadAsync(CurrentToken());

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

  private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => Render();

  private CancellationToken CurrentToken()
  {
    pageCancellation ??= new CancellationTokenSource();
    return pageCancellation.Token;
  }

  private async Task RunActionAsync(Func<CancellationToken, Task> action)
  {
    try { await action(CurrentToken()).ConfigureAwait(true); }
    catch (OperationCanceledException) { }
  }

  private static string StatusLabel(IntegrityFlagStatus status) => status switch
  {
    IntegrityFlagStatus.Pending => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPending),
    IntegrityFlagStatus.Resolved => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolved),
    _ => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityAll),
  };
}
