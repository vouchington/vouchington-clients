using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class StaffSupportThreadPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly StaffSupportThreadViewModel viewModel;
  private readonly IDisposable localeSubscription;
  private readonly Dictionary<string, string> unsavedDraftBodies = new(StringComparer.Ordinal);
  private string? threadId;
  private readonly Label subject = new() { FontSize = 24, FontAttributes = FontAttributes.Bold };
  private readonly VerticalStackLayout messages = new() { Spacing = 8, AutomationId = "staff-support-messages" };
  private readonly Editor reply = new() { MinimumHeightRequest = 90, AutomationId = "staff-support-reply" };
  private readonly Label draftStatus = UiCopy.Bind(new Label { IsVisible = false }, Label.TextProperty, UiMessageKey.NativeSwiftSupportWaitingForAiDraft);
  private readonly Label error = new() { TextColor = Colors.DarkRed, AutomationId = "staff-support-error" };
  private readonly Button retry;
  private readonly Button assignButton;
  private readonly Button resolveButton;
  private readonly Button reopenButton;
  private readonly Button olderButton;
  private readonly Button draftButton;
  private readonly Button saveReplyButton;
  private readonly Label replyDeliveryNote = UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftSupportSavedReplyNotEmailed);

  public StaffSupportThreadPage(
      StaffSupportThreadViewModel viewModel,
      IUiLocaleController localeController)
  {
    this.viewModel = viewModel;
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    retry = Button(UiMessageKey.NativeCommonRetry, RetryAsync);
    retry.AutomationId = "staff-support-thread-retry";
    retry.IsVisible = false;
    assignButton = Button(UiMessageKey.NativeSwiftSupportAssignToMe, AssignAsync);
    resolveButton = Button(UiMessageKey.NativeDotnetCsharpCommunitiesResolve, ResolveAsync);
    reopenButton = Button(UiMessageKey.NativeDotnetCsharpCommunitiesReopen, ReopenAsync);
    olderButton = Button(UiMessageKey.NativeSwiftCommonLoadMore, OlderAsync);
    draftButton = Button(UiMessageKey.NativeSwiftSupportGenerateAiDraft, DraftAsync);
    saveReplyButton = Button(UiMessageKey.NativeSwiftSupportSaveOutboundReply, SaveReplyAsync);
    SetDynamicResource(TitleProperty, UiMessageKey.NativeSwiftSupportSupport.Value);
    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          subject,
          error,
          retry,
          assignButton,
          resolveButton,
          reopenButton,
          olderButton,
          messages,
          draftButton,
          draftStatus,
          reply,
          replyDeliveryNote,
          saveReplyButton,
        },
      },
    };
  }

  public void SetContext(string id) => threadId = id;
  public async Task ApplyRouteAsync(string id)
  {
    SetContext(id);
    await RunAsync(() => viewModel.LoadAsync(id)).ConfigureAwait(true);
  }
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (threadId is null) return;
    await RunAsync(() => viewModel.LoadAsync(threadId)).ConfigureAwait(true);
  }

  private void Refresh()
  {
    subject.Text = viewModel.Thread?.Subject;
    error.Text = viewModel.ErrorMessage ?? string.Empty;
    retry.IsVisible = !string.IsNullOrWhiteSpace(viewModel.ErrorMessage);
    retry.IsEnabled = !viewModel.IsBusy;
    draftStatus.IsVisible = viewModel.IsWaitingForDraft;
    assignButton.IsVisible = viewModel.CanAssign;
    resolveButton.IsVisible = viewModel.CanResolve;
    reopenButton.IsVisible = viewModel.CanReopen;
    olderButton.IsVisible = viewModel.HasOlderMessages;
    draftButton.IsVisible = viewModel.CanGenerateDraft;
    reply.IsVisible = viewModel.CanReply;
    replyDeliveryNote.IsVisible = viewModel.CanReply;
    saveReplyButton.IsVisible = viewModel.CanReply;
    foreach (var button in new[] { assignButton, resolveButton, reopenButton, olderButton, draftButton, saveReplyButton })
    {
      button.IsEnabled = !viewModel.IsBusy;
    }
    messages.Children.Clear();
    foreach (var message in viewModel.Messages) messages.Children.Add(BuildMessage(message));
  }

  private View BuildMessage(SupportMessage message)
  {
    var canEdit = viewModel.CanReply && StaffSupportThreadViewModel.CanEditDraft(message);
    var editor = new Editor { Text = unsavedDraftBodies.GetValueOrDefault(message.Id, message.BodyText), IsReadOnly = !canEdit || viewModel.IsBusy };
    var direction = new Label { Text = UiCopy.Resolve(UiTaxonomy.MessageDirection(message.Direction)) };
    var stack = new VerticalStackLayout { Padding = 8, Children = { direction, editor } };
    if (canEdit)
    {
      var saveDraft = Button(CommonSaveKey, (_, _) => _ = SaveDraftClickedAsync(message, editor));
      saveDraft.IsEnabled = !viewModel.IsBusy;
      stack.Children.Add(saveDraft);
      var approve = ActionButton(UiMessageKey.NativeSwiftModerationAppealsApprove, async () => { if (await ConfirmAsync(UiMessageKey.NativeSwiftSupportApproveDraftConfirmationTitle, UiMessageKey.NativeSwiftModerationAppealsApprove).ConfigureAwait(true)) await viewModel.ApproveAsync(message).ConfigureAwait(true); });
      void RefreshApproveAvailability() => approve.IsEnabled = !viewModel.IsBusy && string.Equals(editor.Text, message.BodyText, StringComparison.Ordinal);
      editor.TextChanged += (_, _) =>
      {
        unsavedDraftBodies[message.Id] = editor.Text ?? string.Empty;
        RefreshApproveAvailability();
      };
      RefreshApproveAvailability();
      stack.Children.Add(approve);
    }
    else if (viewModel.CanReply && StaffSupportThreadViewModel.CanSendDraft(message))
    {
      stack.Children.Add(ActionButton(UiMessageKey.NativeSwiftCommonSend, async () => { if (await ConfirmAsync(UiMessageKey.NativeSwiftSupportSendDraftConfirmationTitle, UiMessageKey.NativeSwiftCommonSend).ConfigureAwait(true)) await viewModel.SendAsync(message).ConfigureAwait(true); }));
    }
    return stack;
  }

  private UiMessageKey CommonSaveKey => UiMessageKey.CommonSave;
  private Button ActionButton(UiMessageKey key, Func<Task> action)
  {
    var button = Button(key, (_, _) => _ = InvokeActionAsync(action));
    button.IsEnabled = !viewModel.IsBusy;
    return button;
  }

  private async Task SaveDraftClickedAsync(SupportMessage message, Editor editor)
  {
    var body = editor.Text ?? string.Empty;
    unsavedDraftBodies[message.Id] = body;
    await viewModel.SaveDraftAsync(message, body).ConfigureAwait(true);
    if (viewModel.ErrorMessage is null) unsavedDraftBodies.Remove(message.Id);
    Refresh();
  }

  private async Task InvokeActionAsync(Func<Task> action)
  {
    await action().ConfigureAwait(true);
    Refresh();
  }
  private async Task<bool> ConfirmAsync(UiMessageKey titleKey, UiMessageKey actionKey)
  {
    var action = UiCopy.Localize(actionKey);
    return await DisplayAlertAsync(
      UiCopy.Localize(titleKey),
      action,
      action,
      UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true);
  }
  private async void AssignAsync(object? s, EventArgs e) { await viewModel.AssignAsync().ConfigureAwait(true); Refresh(); }
  private async void RetryAsync(object? s, EventArgs e) { if (threadId is not null) await RunAsync(() => viewModel.LoadAsync(threadId)).ConfigureAwait(true); }
  private async void ResolveAsync(object? s, EventArgs e) { if (await ConfirmAsync(UiMessageKey.NativeSwiftSupportResolveThreadConfirmationTitle, UiMessageKey.NativeDotnetCsharpCommunitiesResolve).ConfigureAwait(true)) { await viewModel.SetResolvedAsync(true).ConfigureAwait(true); Refresh(); } }
  private async void ReopenAsync(object? s, EventArgs e) { if (await ConfirmAsync(UiMessageKey.NativeSwiftSupportReopenThreadConfirmationTitle, UiMessageKey.NativeDotnetCsharpCommunitiesReopen).ConfigureAwait(true)) { await viewModel.SetResolvedAsync(false).ConfigureAwait(true); Refresh(); } }
  private async void OlderAsync(object? s, EventArgs e) { await viewModel.LoadOlderAsync().ConfigureAwait(true); Refresh(); }
  private async void DraftAsync(object? s, EventArgs e) { await viewModel.GenerateDraftAsync().ConfigureAwait(true); Refresh(); }
  private async void SaveReplyAsync(object? s, EventArgs e) { viewModel.ReplyText = reply.Text ?? string.Empty; await viewModel.SaveOutboundReplyAsync().ConfigureAwait(true); reply.Text = viewModel.ReplyText; Refresh(); }
  private Task RunAsync(Func<Task> action) =>
      StaffSupportPageOperation.RunAsync(action, viewModel.ReportUnexpectedError, Refresh);
  private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => Refresh();
  public void OnUiLocaleChanged() => Refresh();
  public void Dispose()
  {
    viewModel.PropertyChanged -= ViewModelPropertyChanged;
    localeSubscription.Dispose();
  }
  private static Button Button(UiMessageKey key, EventHandler clicked) { var button = UiCopy.Bind(new Button(), Button.TextProperty, key); button.Clicked += clicked; return button; }
}
