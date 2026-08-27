using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ChatListPage : ContentPage
{
  private readonly ChatListViewModel viewModel;
  private readonly IServiceProvider serviceProvider;

  public ChatListPage(ChatListViewModel viewModel, IServiceProvider serviceProvider)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    BindingContext = viewModel;
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadNextPageAsync().ConfigureAwait(true);

  private async void OnStartClicked(object? sender, EventArgs e)
  {
    try
    {
      var message = NewMessageEditor.Text?.Trim() ?? string.Empty;
      NewMessageEditor.Text = string.Empty;

      var page = serviceProvider.GetRequiredService<ChatConversationPage>();
      page.SetContext(initialMessage: message.Length == 0 ? null : message);
      await Navigation.PushAsync(page).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    try
    {
      if (sender is Button { CommandParameter: ChatConversationRow row })
      {
        var page = serviceProvider.GetRequiredService<ChatConversationPage>();
        page.SetContext(row.Id, row.Title);
        await Navigation.PushAsync(page).ConfigureAwait(true);
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    try
    {
      if (sender is Button { CommandParameter: ChatConversationRow row })
      {
        if (!await DisplayAlertAsync(
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsDeleteConversation),
            UiCopy.Format(
                UiMessageKey.NativeDotnetCsharpDialogsDeleteNamedConversation,
                ("title", UiText.Verbatim(row.DisplayTitle))),
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDelete),
            UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true))
        {
          return;
        }

        await viewModel.DeleteConversationAsync(row).ConfigureAwait(true);
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
