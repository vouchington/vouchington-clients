using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class StaffSupportContactPage : ContentPage, IDisposable
{
  private readonly StaffSupportContactViewModel viewModel;
  private readonly IServiceProvider services;
  private string? contactId;
  private readonly Label email = new() { FontSize = 24, FontAttributes = FontAttributes.Bold };
  private readonly Label name = new();
  private readonly Label notes = new();
  private readonly Label error = new() { AutomationId = "staff-support-contact-error", TextColor = Colors.DarkRed };
  private readonly CollectionView threads = new() { AutomationId = "staff-support-contact-threads" };
  private readonly Button retry;
  private readonly Button more;
  public StaffSupportContactPage(StaffSupportContactViewModel viewModel, IServiceProvider services)
  {
    this.viewModel = viewModel;
    this.services = services;
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    threads.ItemTemplate = new DataTemplate(BuildThreadRow);
    more = UiCopy.Bind(new Button { AutomationId = "staff-support-contact-load-more" }, Button.TextProperty, UiMessageKey.NativeSwiftCommonLoadMore);
    more.Clicked += MoreAsync;
    more.IsVisible = false;
    more.IsEnabled = false;
    retry = UiCopy.Bind(new Button { AutomationId = "staff-support-contact-retry" }, Button.TextProperty, UiMessageKey.NativeCommonRetry);
    retry.Clicked += RetryAsync;
    Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { email, name, notes, error, retry, threads, more } };
  }
  public void SetContext(string id) => contactId = id;
  public async Task ApplyRouteAsync(string id)
  {
    SetContext(id);
    await RunAsync(() => viewModel.LoadAsync(id)).ConfigureAwait(true);
  }
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (contactId is null) return;
    await RunAsync(() => viewModel.LoadAsync(contactId)).ConfigureAwait(true);
  }
  private async void MoreAsync(object? sender, EventArgs args) => await RunAsync(() => viewModel.LoadMoreAsync()).ConfigureAwait(true);
  private async void RetryAsync(object? sender, EventArgs args) { if (contactId is not null) await RunAsync(() => viewModel.LoadAsync(contactId)).ConfigureAwait(true); }
  private View BuildThreadRow()
  {
    var subject = new Label();
    subject.SetBinding(Label.TextProperty, nameof(SupportThread.Subject));
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpOpen);
    open.SetBinding(Button.CommandParameterProperty, new Binding("."));
    open.Clicked += OpenThreadAsync;
    return new VerticalStackLayout { Padding = 8, Children = { subject, open } };
  }
  private async void OpenThreadAsync(object? sender, EventArgs args)
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
    email.Text = viewModel.Contact?.EmailAddress;
    name.Text = viewModel.Contact?.Name;
    notes.Text = viewModel.Contact?.Notes;
    error.Text = viewModel.ErrorMessage ?? string.Empty;
    retry.IsVisible = !string.IsNullOrWhiteSpace(viewModel.ErrorMessage);
    threads.ItemsSource = viewModel.Threads;
    more.IsVisible = viewModel.HasMore;
    more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading;
  }
  private void ViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();
  public void Dispose() => viewModel.PropertyChanged -= ViewModelPropertyChanged;
}
