using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Friends;

namespace Voucha.Client.App.Pages;

public partial class FriendRecommendationsPage : ContentPage, IDisposable
{
  private readonly FriendRecommendationsViewModel viewModel;
  private bool paginationHandlerAttached;
  private bool hadNavigationParent;
  private bool disposed;

  public FriendRecommendationsPage(FriendRecommendationsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnRefreshing(object? sender, EventArgs e) =>
      await viewModel.ReloadAsync().ConfigureAwait(true);

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnConnectAccountsClicked(object? sender, EventArgs e)
  {
    if (Shell.Current is AppShell appShell)
    {
      await appShell.OpenNativePathAsync("/my/identity").ConfigureAwait(true);
    }
  }

  private async void OnDismissedRecommendationsClicked(object? sender, EventArgs e)
  {
    if (Shell.Current is AppShell appShell)
    {
      await appShell.OpenNativePathAsync("/my/friend-recommendations/dismissed")
          .ConfigureAwait(true);
    }
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private void OnLoaded(object? sender, EventArgs e) => AttachPaginationHandler();

  private void AttachPaginationHandler()
  {
    if (paginationHandlerAttached) return;
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    paginationHandlerAttached = true;
  }

  private async void OnFollowClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: FriendRecommendationRow row })
    {
      await viewModel.FollowAsync(row).ConfigureAwait(true);
    }
  }

  private async void OnDismissClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: FriendRecommendationRow row })
    {
      await viewModel.DismissAsync(row).ConfigureAwait(true);
    }
  }

  private void OnUnloaded(object? sender, EventArgs e)
  {
    if (!paginationHandlerAttached) return;
    PaginationControl.LoadNextPageRequested -= OnLoadNextPageRequested;
    paginationHandlerAttached = false;
  }

  protected override void OnParentSet()
  {
    base.OnParentSet();
    if (Parent is not null)
    {
      hadNavigationParent = true;
    }
    else if (hadNavigationParent)
    {
      Dispose();
    }
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    Loaded -= OnLoaded;
    Unloaded -= OnUnloaded;
    OnUnloaded(this, EventArgs.Empty);
    viewModel.Dispose();
    GC.SuppressFinalize(this);
  }
}
