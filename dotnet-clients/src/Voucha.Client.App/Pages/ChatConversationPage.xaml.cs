using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ChatConversationPage : ContentPage
{
  private readonly ChatConversationViewModel viewModel;
  private readonly IChatService chatService;
  private string? conversationId;
  private string? conversationTitle;
  private string? initialMessage;
  private bool hasLoaded;

  public ChatConversationPage(ChatConversationViewModel viewModel, IChatService chatService)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    InitializeComponent();
    BindingContext = viewModel;
  }

  public void SetContext(string? conversationId = null, string? conversationTitle = null, string? initialMessage = null)
  {
    this.conversationId = conversationId;
    this.conversationTitle = conversationTitle;
    this.initialMessage = initialMessage;
  }

  public async Task ApplyRouteAsync(string? conversationId, string? conversationTitle = null)
  {
    SetContext(conversationId, conversationTitle);
    hasLoaded = true;
    await LoadConversationAsync().ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await LoadConversationAsync().ConfigureAwait(true);
      if (!hasLoaded && !string.IsNullOrWhiteSpace(initialMessage))
      {
        var message = initialMessage;
        initialMessage = null;
        await viewModel.SendAsync(message!).ConfigureAwait(true);
        conversationId = viewModel.ConversationId;
      }

      hasLoaded = true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnRenameClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.RenameAsync(TitleEntry.Text ?? string.Empty).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnLoadOlderMessagesClicked(object? sender, EventArgs e)
  {
    await viewModel.LoadOlderMessagesAsync().ConfigureAwait(true);
  }

  private async void OnGenerateTitleClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.GenerateTitleAsync().ConfigureAwait(true);
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
      if (!await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsDeleteConversation),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpThisCannotBeUndone),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDelete),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
      {
        return;
      }

      await viewModel.DeleteAsync().ConfigureAwait(true);
      if (viewModel.IsDeleted)
      {
        if (Shell.Current is not null)
        {
          await Shell.Current.GoToAsync("//chat").ConfigureAwait(true);
        }
        else
        {
          await Navigation.PopAsync().ConfigureAwait(true);
        }
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnSendClicked(object? sender, EventArgs e)
  {
    try
    {
      var message = MessageEditor.Text?.Trim() ?? string.Empty;
      if (message.Length == 0) return;
      var sent = await viewModel.TrySendAsync(message).ConfigureAwait(true);
      conversationId = viewModel.ConversationId;
      if (!sent) return;
      MessageEditor.Text = string.Empty;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private void OnMessageTextChanged(object? sender, TextChangedEventArgs e) =>
      viewModel.NotifyDraftEdited(e.NewTextValue);

  private async void OnStopClicked(object? sender, EventArgs e) => await viewModel.StopStreamingAsync();

  private async void OnProviderSelected(object? sender, EventArgs e) =>
      await viewModel.PersistSelectedProviderAsync();

  private async void OnSetUpWindowsModelClicked(object? sender, EventArgs e)
    => await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync();

  private async Task LoadConversationAsync()
  {
    var title = conversationTitle;
    if (conversationId is { Length: > 0 } && string.IsNullOrWhiteSpace(title))
    {
      try
      {
        title = await ChatListViewModel.ResolveConversationTitleAsync(chatService, conversationId)
            .ConfigureAwait(true);
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        System.Diagnostics.Debug.WriteLine(ex);
      }

      conversationTitle = title;
    }

    await viewModel.LoadAsync(conversationId, title).ConfigureAwait(true);
  }
}
