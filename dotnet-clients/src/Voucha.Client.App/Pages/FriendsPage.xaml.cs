using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Friends;

namespace Voucha.Client.App.Pages;

public partial class FriendsPage : ContentPage
{
  private readonly FriendsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly FriendsRouteContextStore routeContextStore;
  private readonly FriendsPageBinding binding;

  public FriendsPage(
      FriendsViewModel viewModel,
      ISessionStore sessionStore,
      FriendsRouteContextStore routeContextStore)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.routeContextStore = routeContextStore;
    binding = new FriendsPageBinding(viewModel, ReloadCurrentTabAsync);
    BindingContext = binding;
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    binding.AttachSessionChanged(sessionStore);
    viewModel.UpdateCurrentUser(sessionStore.Current.Identity?.Id);
    try
    {
      if (routeContextStore.Consume() is { } requestedTab)
      {
        await SelectTabAsync(requestedTab);
      }
      else
      {
        await viewModel.LoadAsync();
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    binding.DetachSessionChanged(sessionStore);
    base.OnDisappearing();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow tab load failures to escape to the dispatcher.")]
  private async void OnFollowingClicked(object? sender, EventArgs e)
  {
    try
    {
      await SelectTabAsync(FriendsTab.Following);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow tab load failures to escape to the dispatcher.")]
  private async void OnFollowersClicked(object? sender, EventArgs e)
  {
    try
    {
      await SelectTabAsync(FriendsTab.Followers);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow mutation failures to escape to the dispatcher.")]
  private async void OnFollowClicked(object? sender, EventArgs e)
  {
    try
    {
      if (sender is Button { CommandParameter: FriendRow row })
      {
        await viewModel.ToggleFollowAsync(row);
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow page-load failures to escape to the dispatcher.")]
  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private async Task SelectTabAsync(FriendsTab tab)
  {
    binding.SetSelectedTab(tab);
    await viewModel.SelectTabAsync(tab);
  }

  private Task ReloadCurrentTabAsync() => viewModel.ReloadAsync();
}
