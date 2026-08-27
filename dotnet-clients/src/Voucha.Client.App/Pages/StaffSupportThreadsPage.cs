using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class StaffSupportThreadsPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly StaffSupportThreadsViewModel viewModel;
  private readonly IServiceProvider services;
  private readonly Entry search = UiCopy.Bind(new Entry { AutomationId = "staff-support-search" }, Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpSearch);
  private readonly Picker status = new() { AutomationId = "staff-support-status" };
  private readonly CollectionView list = new() { AutomationId = "staff-support-threads" };
  private readonly Label error = new() { AutomationId = "staff-support-threads-error", TextColor = Colors.DarkRed };
  private readonly Button retry;
  private readonly Button more;
  private readonly IDisposable localeSubscription;

  public StaffSupportThreadsPage(
      StaffSupportThreadsViewModel viewModel,
      IServiceProvider services,
      IUiLocaleController localeController)
  {
    this.viewModel = viewModel;
    this.services = services;
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    ConfigureStatusPicker();
    retry = Button(UiMessageKey.NativeCommonRetry, RetryAsync);
    more = Button(UiMessageKey.NativeSwiftCommonLoadMore, LoadMoreAsync);
    more.IsVisible = false;
    more.IsEnabled = false;
    retry.AutomationId = "staff-support-threads-retry";
    more.AutomationId = "staff-support-threads-load-more";
    SetDynamicResource(TitleProperty, UiMessageKey.NativeSwiftSupportSupport.Value);
    list.ItemTemplate = new DataTemplate(BuildRow);
    Content = new VerticalStackLayout
    {
      Padding = 16,
      Spacing = 12,
      Children =
      {
        UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftSupportStatus),
        new HorizontalStackLayout { Children = { search, status, Button(UiMessageKey.NativeDotnetCsharpSearch, SearchAsync) } },
        error,
        retry,
        list,
        more,
      },
    };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await RunAsync(() => viewModel.LoadAsync()).ConfigureAwait(true);
  }

  private View BuildRow()
  {
    var subject = new Label { FontAttributes = FontAttributes.Bold };
    subject.SetBinding(Label.TextProperty, nameof(SupportThread.Subject));
    var open = Button(UiMessageKey.NativeDotnetCsharpOpen, OpenAsync);
    open.SetBinding(Button.CommandParameterProperty, new Binding("."));
    return new VerticalStackLayout { Padding = 8, Children = { subject, open } };
  }

  private async void SearchAsync(object? sender, EventArgs args)
  {
    await RunAsync(async () =>
    {
      viewModel.Query = search.Text;
      viewModel.Status = (status.SelectedItem as StaffSupportStatusOption)?.Status;
      await viewModel.LoadAsync().ConfigureAwait(true);
    }).ConfigureAwait(true);
  }

  private async void LoadMoreAsync(object? sender, EventArgs args) => await RunAsync(() => viewModel.LoadMoreAsync()).ConfigureAwait(true);
  private async void RetryAsync(object? sender, EventArgs args) => await RunAsync(() => viewModel.LoadAsync()).ConfigureAwait(true);
  private async void OpenAsync(object? sender, EventArgs args)
  {
    if (sender is not Button { CommandParameter: SupportThread thread }) return;
    var page = services.GetRequiredService<StaffSupportThreadPage>();
    page.SetContext(thread.Id);
    await Navigation.PushAsync(page).ConfigureAwait(true);
  }
  private Task RunAsync(Func<Task> action) =>
      StaffSupportPageOperation.RunAsync(action, viewModel.ReportUnexpectedError, Refresh);
  private void Refresh()
  {
    list.ItemsSource = viewModel.Threads;
    error.Text = viewModel.ErrorMessage ?? string.Empty;
    retry.IsVisible = !string.IsNullOrWhiteSpace(viewModel.ErrorMessage);
    more.IsVisible = viewModel.HasMore;
    more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading;
  }
  private void ConfigureStatusPicker()
  {
    var selected = status.SelectedItem is StaffSupportStatusOption option ? option.Status : viewModel.Status;
    status.ItemsSource = StatusOptions();
    status.ItemDisplayBinding = new Binding(nameof(StaffSupportStatusOption.Label));
    status.SelectedIndex = Array.FindIndex(StatusOptions(), option => option.Status == selected);
  }
  private static StaffSupportStatusOption[] StatusOptions() =>
  [
    new(null, UiCopy.Localize(UiMessageKey.NativeSwiftCommonAll)),
    new(StaffSupportThreadStatusFilter.Open, UiCopy.Localize(UiMessageKey.NativeTaxonomySupportOpen)),
    new(StaffSupportThreadStatusFilter.Assigned, UiCopy.Localize(UiMessageKey.NativeSwiftPresentationValuesAssigned)),
    new(StaffSupportThreadStatusFilter.Resolved, UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolved)),
  ];
  public void OnUiLocaleChanged() => ConfigureStatusPicker();
  private void ViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();
  public void Dispose() { viewModel.PropertyChanged -= ViewModelPropertyChanged; localeSubscription.Dispose(); }
  private static Button Button(UiMessageKey key, EventHandler clicked) { var button = UiCopy.Bind(new Button(), Button.TextProperty, key); button.Clicked += clicked; return button; }
}

internal sealed record StaffSupportStatusOption(StaffSupportThreadStatusFilter? Status, string Label);
