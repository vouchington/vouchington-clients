using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Auth;
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
  private bool suppressDraftChange;
  private long contextVersion;

  public ChatConversationPage(
      ChatConversationViewModel viewModel, IChatService chatService, MemberMcpSession? mcpSession = null,
      ISessionStore? sessionStore = null)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    if (sessionStore is not null)
    {
      memberSession = new ChatConversationMemberSession(sessionStore, viewModel, action =>
      {
        if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(action);
        else action();
      });
      memberSession.MemberChanged += OnMemberChanged;
    }
    InitializeComponent();
    BindingContext = viewModel;
    if (mcpSession is not null) McpControlsHost.Content = new ChatMemberMcpControlsView(mcpSession);
  }

  public void SetContext(string? conversationId = null, string? conversationTitle = null, string? initialMessage = null)
  {
    _ = Interlocked.Increment(ref contextVersion);
    this.conversationId = conversationId;
    this.conversationTitle = conversationTitle;
    this.initialMessage = initialMessage;
  }

  public async Task ApplyRouteAsync(string? conversationId, string? conversationTitle = null)
  {
    if (viewModel.ConversationId != conversationId) MessageEditor.Text = string.Empty;
    SetContext(conversationId, conversationTitle);
    hasLoaded = true;
    await LoadConversationAsync().ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    memberSession?.Attach();
    var version = contextVersion;
    try
    {
      await LoadConversationAsync().ConfigureAwait(true);
      if (version != contextVersion) return;
      if (!hasLoaded && !string.IsNullOrWhiteSpace(initialMessage))
      {
        await SendInitialMessageAsync().ConfigureAwait(true);
      }

      hasLoaded = true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async Task SendInitialMessageAsync()
  {
    var message = initialMessage;
    if (string.IsNullOrWhiteSpace(message)) return;
    initialMessage = null;
    var version = contextVersion;
    var sent = await viewModel.TrySendAsync(message).ConfigureAwait(true);
    if (version != contextVersion) return;
    conversationId = viewModel.ConversationId;
    if (!sent && !viewModel.PossibleToolSideEffect && string.IsNullOrWhiteSpace(MessageEditor.Text))
    {
      MessageEditor.Text = message;
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
      var version = contextVersion;
      var sent = await viewModel.TrySendAsync(message).ConfigureAwait(true);
      if (version != contextVersion) return;
      conversationId = viewModel.ConversationId;
      if (!sent && !viewModel.PossibleToolSideEffect) return;
      suppressDraftChange = viewModel.PossibleToolSideEffect;
      try { MessageEditor.Text = string.Empty; }
      finally { suppressDraftChange = false; }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private void OnMessageTextChanged(object? sender, TextChangedEventArgs e)
  {
    if (!suppressDraftChange) viewModel.NotifyDraftEdited(e.NewTextValue);
  }

  private async void OnStopClicked(object? sender, EventArgs e) => await viewModel.StopStreamingAsync();

  private async void OnProviderSelected(object? sender, EventArgs e) =>
      await viewModel.PersistSelectedProviderAsync();

  private async void OnSetUpWindowsModelClicked(object? sender, EventArgs e)
    => await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync();

}
