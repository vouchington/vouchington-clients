using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal sealed class FollowerDistributionSendPage : ContentPage
{
  private readonly FollowerDistributionViewModel state;
  private readonly string userId;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly VerticalStackLayout recipients = new() { Spacing = 6 };
  private readonly VerticalStackLayout selectedRecipients = new() { Spacing = 6 };
  private readonly Entry search = new() { Placeholder = T(UiMessageKey.NativeSwiftFollowerDistributionSearchFollowers) };
  private readonly Button selected = new() { Text = T(UiMessageKey.NativeSwiftFollowerDistributionSelectedFollowers) };
  private readonly Button all = new() { Text = T(UiMessageKey.NativeSwiftFollowerDistributionAllFollowers) };
  private readonly Button send = new() { Text = UiCopy.Localize(UiMessageKey.NativeSwiftCommonSend) };
  private readonly Button cancel = new() { AutomationId = "follower-distribution-cancel", Text = UiCopy.Localize(UiMessageKey.CommonCancel) };
  private readonly Button more = new() { Text = T(UiMessageKey.NativeSwiftFollowerDistributionLoadMoreFollowers) };
  private readonly Label error = new() { TextColor = Colors.IndianRed };
  private bool isSessionChangedAttached;
  private bool isDismissing;

  public FollowerDistributionSendPage(FollowerDistributionViewModel state, string userId, ISessionStore sessionStore)
  {
    this.state = state; this.userId = userId; this.sessionStore = sessionStore;
    sessionChangedHandler = (_, args) =>
    {
      if (args.Snapshot.Identity?.Id != userId)
        MainThread.BeginInvokeOnMainThread(() => _ = DismissAsync());
    };
    Title = T(UiMessageKey.NativeSwiftFollowerDistributionSendToFollowers);
    all.Clicked += (_, _) => { state.SetAudience(false); Refresh(); };
    selected.Clicked += async (_, _) =>
    {
      state.SetAudience(true);
      Refresh();
      await state.SearchFollowersAsync(userId, search.Text ?? string.Empty);
      Refresh();
    };
    search.TextChanged += (_, args) => _ = SearchAsync(args.NewTextValue ?? string.Empty);
    more.Clicked += async (_, _) => { await state.LoadMoreFollowersAsync(); Refresh(); };
    send.Clicked += async (_, _) => await SendAsync();
    cancel.Clicked += OnCancelClicked;
    Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 10, Children = { new HorizontalStackLayout { Children = { all, selected } }, search, selectedRecipients, recipients, more, error, new HorizontalStackLayout { Spacing = 8, Children = { cancel, send } } } } };
    Refresh();
  }

  private async Task SearchAsync(string value) { await state.SearchFollowersAsync(userId, value); Refresh(); }

  private async Task SendAsync()
  {
    if (await state.SubmitAsync()) { await DismissAsync(); return; }
    Refresh();
  }

  private async void OnCancelClicked(object? sender, EventArgs args) =>
      await DismissAsync().ConfigureAwait(true);

  private async Task DismissAsync()
  {
    if (isDismissing) return;
    isDismissing = true;
    await Navigation.PopModalAsync().ConfigureAwait(true);
  }

  private void Refresh()
  {
    search.IsVisible = state.IsSelectedAudience;
    selectedRecipients.IsVisible = state.IsSelectedAudience && state.SelectedRecipients.Count > 0;
    recipients.IsVisible = state.IsSelectedAudience;
    more.IsVisible = state.IsSelectedAudience && state.HasMore;
    send.IsEnabled = state.CanSubmit;
    error.Text = state.Error is null
        ? string.Empty
        : T(UiMessageKey.NativeSwiftFollowerDistributionUnableToSend);
    recipients.Children.Clear();
    foreach (var user in state.Followers) recipients.Children.Add(Recipient(user));
    selectedRecipients.Children.Clear();
    foreach (var user in state.SelectedRecipients) selectedRecipients.Children.Add(SelectedRecipient(user));
  }

  private View Recipient(User user)
  {
    var checkbox = new CheckBox { IsChecked = state.SelectedIds.Contains(user.Id) };
    checkbox.CheckedChanged += (_, _) =>
    {
      var changed = state.ToggleSelection(user);
      Refresh();
      if (!changed) error.Text = T(UiMessageKey.NativeSwiftFollowerDistributionUpToOneHundredFollowers);
    };
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children = { checkbox, new Label { Text = Name(user), VerticalOptions = LayoutOptions.Center } }
    };
  }

  private View SelectedRecipient(User user)
  {
    var remove = new Button { Text = T(UiMessageKey.NativeSwiftCommonRemove) };
    remove.Clicked += (_, _) => { state.ToggleSelection(user.Id); Refresh(); };
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children = { new Label { Text = Name(user), VerticalOptions = LayoutOptions.Center }, remove }
    };
  }

  private static string Name(User user) => user.Name ?? user.Username ?? user.Id;
  private static string T(UiMessageKey key) => UiCopy.Localize(key);

  protected override void OnAppearing()
  {
    base.OnAppearing();
    AttachSessionChanged();
  }

  protected override void OnDisappearing()
  {
    DetachSessionChanged();
    state.Dispose();
    base.OnDisappearing();
  }

  private void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  private void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }
}
