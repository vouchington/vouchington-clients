using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class StaffSupportContactsPage : ContentPage, IDisposable
{
  private readonly StaffSupportContactsViewModel viewModel;
  private readonly IServiceProvider services;
  private readonly Entry search = UiCopy.Bind(new Entry { AutomationId = "staff-support-contact-search" }, Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpSearch);
  private readonly CollectionView list = new() { AutomationId = "staff-support-contacts" };
  private readonly Label error = new() { AutomationId = "staff-support-contacts-error", TextColor = Colors.DarkRed };
  private readonly Button retry;
  private readonly Button more;
  public StaffSupportContactsPage(StaffSupportContactsViewModel viewModel, IServiceProvider services)
  {
    this.viewModel = viewModel;
    this.services = services;
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    retry = Button(UiMessageKey.NativeCommonRetry, RetryAsync);
    more = Button(UiMessageKey.NativeSwiftCommonLoadMore, MoreAsync);
    more.IsVisible = false;
    more.IsEnabled = false;
    retry.AutomationId = "staff-support-contacts-retry";
    more.AutomationId = "staff-support-contacts-load-more";
    list.ItemTemplate = new DataTemplate(BuildRow);
    Content = new VerticalStackLayout
    {
      Padding = 16,
      Children = { new HorizontalStackLayout { Children = { search, Button(UiMessageKey.NativeDotnetCsharpSearch, SearchAsync) } }, error, retry, list, more },
    };
  }
  protected override async void OnAppearing() { base.OnAppearing(); await RunAsync(() => viewModel.LoadAsync()).ConfigureAwait(true); }
  private View BuildRow()
  {
    var email = new Label { FontAttributes = FontAttributes.Bold };
    email.SetBinding(Label.TextProperty, nameof(SupportContact.EmailAddress));
    var open = Button(UiMessageKey.NativeDotnetCsharpOpen, OpenAsync);
    open.SetBinding(Button.CommandParameterProperty, new Binding("."));
    return new VerticalStackLayout { Padding = 8, Children = { email, open } };
  }
  private async void SearchAsync(object? sender, EventArgs args) => await RunAsync(async () => { viewModel.Query = search.Text; await viewModel.LoadAsync().ConfigureAwait(true); }).ConfigureAwait(true);
  private async void MoreAsync(object? sender, EventArgs args) => await RunAsync(() => viewModel.LoadMoreAsync()).ConfigureAwait(true);
  private async void RetryAsync(object? sender, EventArgs args) => await RunAsync(() => viewModel.LoadAsync()).ConfigureAwait(true);
  private async void OpenAsync(object? sender, EventArgs args)
  {
    if (sender is not Button { CommandParameter: SupportContact contact }) return;
    var page = services.GetRequiredService<StaffSupportContactPage>(); page.SetContext(contact.Id); await Navigation.PushAsync(page).ConfigureAwait(true);
  }
  private Task RunAsync(Func<Task> action) =>
      StaffSupportPageOperation.RunAsync(action, viewModel.ReportUnexpectedError, Refresh);
  private void Refresh() { list.ItemsSource = viewModel.Contacts; error.Text = viewModel.ErrorMessage ?? string.Empty; retry.IsVisible = !string.IsNullOrWhiteSpace(viewModel.ErrorMessage); more.IsVisible = viewModel.HasMore; more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading; }
  private void ViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();
  public void Dispose() => viewModel.PropertyChanged -= ViewModelPropertyChanged;
  private static Button Button(UiMessageKey key, EventHandler clicked) { var button = UiCopy.Bind(new Button(), Button.TextProperty, key); button.Clicked += clicked; return button; }
}
