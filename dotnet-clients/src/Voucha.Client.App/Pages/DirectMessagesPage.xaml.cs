using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Messages;

namespace Voucha.Client.App.Pages;

public partial class DirectMessagesPage : ContentPage
{
  private readonly DirectMessagesViewModel viewModel;
  private readonly IServiceProvider serviceProvider;
  private readonly List<DirectMessageUserRow> selectedRecipients = [];
  private NativeRouteMatch? routeMatch;
  private string? appliedRouteKey;
  private bool isSendingMessage;

  public DirectMessagesPage(
      DirectMessagesViewModel viewModel,
      IServiceProvider serviceProvider,
      NativeRouteMatch? initialRouteMatch = null)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.serviceProvider = serviceProvider;
    routeMatch = initialRouteMatch;
    BindingContext = viewModel;
    ConversationPaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    UpdateRecipientSelectionUi();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadInboxAsync();
      await ApplyRouteMatchAsyncCore();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnConversationSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is DirectConversationRow row)
    {
      var previousConversationId = viewModel.SelectedConversationId;
      appliedRouteKey = null;
      if (await RunSafelyAsync(() => viewModel.SelectConversationAsync(row.Id)) &&
          string.Equals(viewModel.SelectedConversationId, row.Id, StringComparison.Ordinal) &&
          !string.Equals(previousConversationId, row.Id, StringComparison.Ordinal))
      {
        MessageEditor.Text = string.Empty;
      }
    }
  }

  private async void OnSendClicked(object? sender, EventArgs e)
  {
    if (isSendingMessage) return;
    isSendingMessage = true;
    var text = MessageEditor.Text ?? string.Empty;
    try
    {
      if (await RunSafelyAsync(() => viewModel.SendMessageAsync(text)) &&
          string.Equals(MessageEditor.Text, text, StringComparison.Ordinal)) MessageEditor.Text = string.Empty;
    }
    finally
    {
      isSendingMessage = false;
    }
  }

  private void OnRemainingConversationsThresholdReached(object? sender, EventArgs e) =>
      ConversationPaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.LoadMoreConversationsAsync());

  private async void OnLoadMoreMessagesClicked(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.LoadMoreMessagesAsync());

  private async void OnRecipientSearchClicked(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.SearchUsersAsync(RecipientSearchEntry.Text ?? string.Empty));

  private async void OnParticipantSearchClicked(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.SearchParticipantUsersAsync(ParticipantSearchEntry.Text ?? string.Empty));

  private void OnAddRecipientClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: DirectMessageUserRow user } &&
        selectedRecipients.All(row => row.Id != user.Id))
    {
      selectedRecipients.Add(user);
      UpdateRecipientSelectionUi();
    }
  }

  private void OnClearRecipientsClicked(object? sender, EventArgs e)
  {
    selectedRecipients.Clear();
    UpdateRecipientSelectionUi();
  }

  private async void OnAddParticipantClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: DirectMessageUserRow user })
    {
      await RunSafelyAsync(() => viewModel.AddParticipantAsync(user.Id));
    }
  }

  private async void OnCreateConversationClicked(object? sender, EventArgs e)
  {
    if (!CreateConversationButton.IsEnabled)
    {
      return;
    }

    CreateConversationButton.IsEnabled = false;
    var previousConversationId = viewModel.SelectedConversationId;
    var composeText = ComposeEditor.Text ?? string.Empty;
    try
    {
      var createdConversation = await RunSafelyAsync(() => viewModel.CreateConversationAsync(
          selectedRecipients.Select(row => row.Id).ToArray(),
          composeText,
          selectedRecipients.Select(row => row.Username).ToArray()));
      if (!createdConversation)
      {
        if (!string.Equals(previousConversationId, viewModel.SelectedConversationId, StringComparison.Ordinal))
        {
          selectedRecipients.Clear();
          UpdateRecipientSelectionUi();
          MessageEditor.Text = ComposeEditor.Text ?? string.Empty;
          ComposeEditor.Text = string.Empty;
        }
        return;
      }
      selectedRecipients.Clear();
      UpdateRecipientSelectionUi();
      if (!string.Equals(previousConversationId, viewModel.SelectedConversationId, StringComparison.Ordinal))
      {
        MessageEditor.Text = string.Empty;
      }
      if (string.Equals(ComposeEditor.Text, composeText, StringComparison.Ordinal)) ComposeEditor.Text = string.Empty;
    }
    finally
    {
      CreateConversationButton.IsEnabled = true;
    }
  }

  private async void OnRemoveParticipantClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: DirectMessageParticipantRow { UserId: { } userId } })
    {
      await RunSafelyAsync(() => viewModel.RemoveParticipantAsync(userId));
    }
  }

  private async void OnAllowAllMembersClicked(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.UpdateParticipantAddPolicyAsync("all_members"));

  private async void OnOwnerOnlyClicked(object? sender, EventArgs e) =>
      await RunSafelyAsync(() => viewModel.UpdateParticipantAddPolicyAsync("owner_only"));

  private async void OnNotificationsClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<NotificationsPage>());

  private void UpdateRecipientSelectionUi()
  {
    SelectedRecipientsLabel.Text = selectedRecipients.Count == 0
        ? UiCopy.Localize(UiMessageKey.NativeDotnetDirectMessagesNoRecipients)
        : UiCopy.Format(
            UiMessageKey.NativeDotnetDirectMessagesSelectedRecipients,
            ("recipients", UiText.Verbatim(string.Join(", ", selectedRecipients
                .Select(row => UiUserHandle.FromUsername(row.Username).Value)))));
    ClearRecipientsButton.IsVisible = selectedRecipients.Count > 0;
  }
}
