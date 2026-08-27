using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public sealed class NewsFeedsPageBinding : BindableObject
{
  private readonly NewsFeedsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private bool isRefreshing;
  private bool isSessionChangedAttached;

  public NewsFeedsPageBinding(NewsFeedsViewModel viewModel, ISessionStore sessionStore)
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    RefreshCommand = new Command(() => _ = RefreshAsync());
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    viewModel.PropertyChanged += (_, e) =>
    {
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.Items))
      {
        OnPropertyChanged(nameof(Items));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.ErrorMessage))
      {
        OnPropertyChanged(nameof(ErrorMessage));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.HasError))
      {
        OnPropertyChanged(nameof(HasError));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is
          nameof(NewsFeedsViewModel.HasMore) or
          nameof(NewsFeedsViewModel.IsLoadingMore) or
          nameof(NewsFeedsViewModel.HasPaginationError))
      {
        OnPropertyChanged(e.PropertyName);
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsYourFeedSelected))
      {
        OnPropertyChanged(nameof(IsYourFeedSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsAllNewsSelected))
      {
        OnPropertyChanged(nameof(IsAllNewsSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsYourSourcesSelected))
      {
        OnPropertyChanged(nameof(IsYourSourcesSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsAllSourcesSelected))
      {
        OnPropertyChanged(nameof(IsAllSourcesSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsSourceScopeSelected))
      {
        OnPropertyChanged(nameof(IsSourceScopeSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsArticleSourceFeedTypeSelected))
      {
        OnPropertyChanged(nameof(IsArticleSourceFeedTypeSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsPodcastSourceFeedTypeSelected))
      {
        OnPropertyChanged(nameof(IsPodcastSourceFeedTypeSelected));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NewsFeedsViewModel.IsVideoSourceFeedTypeSelected))
      {
        OnPropertyChanged(nameof(IsVideoSourceFeedTypeSelected));
      }
    };
  }

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

  public IReadOnlyList<NewsFeedItem> Items => viewModel.Items;

  public string PageTitle => viewModel.PageTitle;

  public string PrimaryScopeLabel => viewModel.PrimaryScopeLabel;

  public string AllItemsScopeLabel => viewModel.AllItemsScopeLabel;

  public string SourcesScopeLabel => viewModel.SourcesScopeLabel;

  public string AllSourcesScopeLabel => viewModel.AllSourcesScopeLabel;

  public bool IsRefreshing
  {
    get => isRefreshing;
    private set
    {
      if (isRefreshing == value) return;
      isRefreshing = value;
      OnPropertyChanged();
    }
  }

  public string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasError => viewModel.HasError;

  public bool HasMore => viewModel.HasMore;

  public bool IsLoadingMore => viewModel.IsLoadingMore;

  public bool HasPaginationError => viewModel.HasPaginationError;

  public bool CanVote => sessionStore.Current.CanCastPublicVotes();

  public bool CanClearVote => sessionStore.Current.IsAuthenticated && !sessionStore.Current.CanCastPublicVotes();

  public bool CanRelate => sessionStore.Current.IsAuthenticated;

  public bool CanImportExport => sessionStore.Current.IsAuthenticated;

  public bool IsYourFeedSelected => viewModel.IsYourFeedSelected;

  public bool IsAllNewsSelected => viewModel.IsAllNewsSelected;

  public bool IsYourSourcesSelected => viewModel.IsYourSourcesSelected;

  public bool IsAllSourcesSelected => viewModel.IsAllSourcesSelected;

  public bool IsSourceScopeSelected => viewModel.IsSourceScopeSelected;

  public bool IsArticleSourceFeedTypeSelected => viewModel.IsArticleSourceFeedTypeSelected;

  public bool IsPodcastSourceFeedTypeSelected => viewModel.IsPodcastSourceFeedTypeSelected;

  public bool IsVideoSourceFeedTypeSelected => viewModel.IsVideoSourceFeedTypeSelected;

  public ICommand RefreshCommand { get; }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Session refresh failures are logged without disrupting the current native page.")]
  private async Task RefreshAfterSessionChangedAsync()
  {
    OnPropertyChanged(nameof(CanVote));
    OnPropertyChanged(nameof(CanClearVote));
    OnPropertyChanged(nameof(CanRelate));
    OnPropertyChanged(nameof(CanImportExport));
    try
    {
      await viewModel.ReloadAfterSessionChangedAsync(sessionStore.Current.IsAuthenticated).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async Task RefreshAsync()
  {
    if (IsRefreshing || viewModel.IsLoading) return;
    IsRefreshing = true;
    try
    {
      await viewModel.LoadAsync();
    }
    finally
    {
      IsRefreshing = false;
    }
  }
}
