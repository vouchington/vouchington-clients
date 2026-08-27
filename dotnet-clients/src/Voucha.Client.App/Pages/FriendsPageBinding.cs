using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class FriendsPageBinding : LoadablePageBinding<FriendRow>
{
  private readonly FriendsViewModel viewModel;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private FriendsTab selectedTab = FriendsTab.Following;
  private bool isSessionChangedAttached;

  public FriendsPageBinding(FriendsViewModel viewModel, Func<Task> refresh) : base(refresh)
  {
    this.viewModel = viewModel;
    selectedTab = viewModel.SelectedTab;
    sessionChangedHandler = (_, args) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync(args.Snapshot));
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
  }

  public bool IsFollowingSelected => selectedTab == FriendsTab.Following;

  public bool IsFollowersSelected => selectedTab == FriendsTab.Followers;

  public string CurrentTabLower => UiCopy.Localize(CurrentTab == FriendsTab.Following
      ? UiMessageKey.NativeSwiftNavigationTitlesFollowing
      : UiMessageKey.NativeSwiftNavigationTitlesFollowers);

  public string EmptyTitle => UiCopy.Localize(CurrentTab == FriendsTab.Following
      ? UiMessageKey.NativeSwiftFriendsNoFollowing
      : UiMessageKey.NativeSwiftFriendsNoFollowers);

  public string EmptyMessage => UiCopy.Localize(CurrentTab == FriendsTab.Following
      ? UiMessageKey.NativeSwiftFriendsNoFollowingMessage
      : UiMessageKey.NativeSwiftFriendsNoFollowersMessage);

  public FriendsTab CurrentTab
  {
    get => selectedTab;
    private set
    {
      if (selectedTab == value) return;
      selectedTab = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsFollowingSelected));
      OnPropertyChanged(nameof(IsFollowersSelected));
      OnPropertyChanged(nameof(CurrentTabLower));
      OnPropertyChanged(nameof(EmptyTitle));
      OnPropertyChanged(nameof(EmptyMessage));
    }
  }

  public override IReadOnlyList<FriendRow> Items => viewModel.Items;

  public override bool HasError => viewModel.HasError;

  public override string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasMore => viewModel.HasMore;

  public bool PaginationIsLoading => viewModel.IsLoading;

  protected override bool IsLoading => viewModel.IsLoading;

  public void AttachSessionChanged(ISessionStore sessionStore)
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  public void DetachSessionChanged(ISessionStore sessionStore)
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }

  public void SetSelectedTab(FriendsTab tab) => CurrentTab = tab;

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FriendsViewModel.SelectedTab))
    {
      CurrentTab = viewModel.SelectedTab;
    }
    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FriendsViewModel.Items))
    {
      OnPropertyChanged(nameof(CurrentTabLower));
      OnPropertyChanged(nameof(EmptyTitle));
      OnPropertyChanged(nameof(EmptyMessage));
    }

    NotifyLoadStateChanged();
    OnPropertyChanged(nameof(HasMore));
    OnPropertyChanged(nameof(PaginationIsLoading));
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Session-change refresh failures should not escape event dispatch.")]
  private async Task RefreshAfterSessionChangedAsync(SessionSnapshot snapshot)
  {
    try
    {
      viewModel.UpdateCurrentUser(snapshot.Identity?.Id);
      await viewModel.ReloadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
