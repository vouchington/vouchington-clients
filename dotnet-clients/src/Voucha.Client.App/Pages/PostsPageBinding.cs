using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public sealed class PostsPageBinding : LoadablePageBinding<PostRow>
{
  private readonly PostsListViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly Func<Task> refresh;
  private bool isSessionChangedAttached;

  public PostsPageBinding(
      PostsListViewModel viewModel,
      ISessionStore sessionStore,
      Func<Task> refresh) : base(refresh)
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.refresh = refresh;
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    viewModel.PropertyChanged += (_, _) =>
    {
      NotifyLoadStateChanged();
      OnPropertyChanged(nameof(HasMore));
      OnPropertyChanged(nameof(PaginationIsLoading));
    };
  }

  public bool CanVote => sessionStore.Current.CanCastPublicVotes();

  public bool CanClearVote => sessionStore.Current.IsAuthenticated && !sessionStore.Current.CanCastPublicVotes();

  public bool CanRelate => sessionStore.Current.IsAuthenticated;

  public string? CurrentUserId => sessionStore.Current.Identity?.Id;

  public override IReadOnlyList<PostRow> Items => viewModel.Items;

  public override bool HasError => viewModel.HasError;

  public override string? ErrorMessage => viewModel.ErrorMessage;

  public bool CanCompose => sessionStore.Current.IsAuthenticated;

  public bool HasMore => viewModel.HasMore;

  public bool PaginationIsLoading => viewModel.IsLoading;

  protected override bool IsLoading => viewModel.IsLoading;

  public void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  public void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }

  private async Task RefreshAfterSessionChangedAsync()
  {
    OnPropertyChanged(nameof(CanVote));
    OnPropertyChanged(nameof(CanClearVote));
    OnPropertyChanged(nameof(CanRelate));
    OnPropertyChanged(nameof(CurrentUserId));
    OnPropertyChanged(nameof(CanCompose));
    try
    {
      await refresh().ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
