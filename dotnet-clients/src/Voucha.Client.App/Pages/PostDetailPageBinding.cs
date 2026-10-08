using System.Collections.ObjectModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.HnDiscussions;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class PostDetailPageBinding : ObservableObject, IUiLocaleChangeListener
{
  private readonly CommentThreadViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private PostDetailPageRow? rootRow;

  public PostDetailPageBinding(
      CommentThreadViewModel viewModel,
      ISessionStore sessionStore,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IHnDiscussionsSettings? hnDiscussionsSettings = null,
      HnDiscussionsClient? hnDiscussionsClient = null)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    HnDiscussions = new HnDiscussionsViewModel(
        hnDiscussionsSettings ?? new MemoryHnDiscussionsSettings(),
        hnDiscussionsClient ?? new HnDiscussionsClient(new HttpClient()),
        this.localization);
  }

  public HnDiscussionsViewModel HnDiscussions { get; }

  public CommentThreadViewModel ViewModel => viewModel;

  public ObservableCollection<PostDetailPageRow> AncestorRows { get; } = [];

  public ObservableCollection<PostDetailPageRow> CommentRows { get; } = [];

  public PostDetailPageRow? RootRow
  {
    get => rootRow;
    private set => SetProperty(ref rootRow, value);
  }

  public bool HasRoot => RootRow is not null;

  public bool HasAncestors => AncestorRows.Count > 0;

  public bool HasError => viewModel.HasError;

  public string? ErrorMessage => viewModel.ErrorMessage;

  public bool IsLoading => viewModel.IsLoading;

  public bool CanVote => sessionStore.Current.CanCastPublicVotes();

  public bool CanCompose => sessionStore.Current.IsAuthenticated;

  public string? CurrentUserId => sessionStore.Current.Identity?.Id;

  public string Sort
  {
    get => viewModel.Sort;
    set
    {
      if (string.Equals(viewModel.Sort, value, StringComparison.Ordinal)) return;
      viewModel.Sort = value;
      OnPropertyChanged();
      RefreshRows();
    }
  }

  public Task LoadAsync(string? focusedCommentId = null, CancellationToken cancellationToken = default) =>
      LoadInternalAsync(focusedCommentId, cancellationToken);

  public void RefreshRows()
  {
    RefreshCommentMetadata();
    RootRow = viewModel.RootPost is null ? null : BuildRow(viewModel.RootPost, 0, false, false);
    AncestorRows.Clear();
    foreach (var ancestor in viewModel.AncestorPosts)
    {
      AncestorRows.Add(BuildRow(ancestor, 0, false, false));
    }

    var descendantRows = BuildRows(viewModel.Comments, 1).ToArray();
    CommentRows.Clear();
    if (viewModel.FocusedComment is { } focused &&
        !string.Equals(focused.Id, viewModel.RootPost?.Id, StringComparison.Ordinal) &&
        !ContainsComment(viewModel.Comments, focused.Id))
    {
      CommentRows.Add(BuildRow(focused, 1, false, false));
    }
    foreach (var row in descendantRows)
    {
      CommentRows.Add(row);
    }

    OnPropertyChanged(nameof(HasRoot));
    OnPropertyChanged(nameof(HasAncestors));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(IsLoading));
    OnPropertyChanged(nameof(HasMoreDescendants));
    OnPropertyChanged(nameof(IsLoadingMoreDescendants));
    OnPropertyChanged(nameof(HasDescendantPaginationError));
    OnPropertyChanged(nameof(HasMoreAncestors));
    OnPropertyChanged(nameof(IsLoadingMoreAncestors));
    OnPropertyChanged(nameof(HasAncestorPaginationError));
    OnPropertyChanged(nameof(CanLoadMoreAncestors));
    OnPropertyChanged(nameof(AncestorPaginationLabel));
    OnPropertyChanged(nameof(CanVote));
    OnPropertyChanged(nameof(CanCompose));
    OnPropertyChanged(nameof(CurrentUserId));
  }

  public void RefreshViewerState()
  {
    OnPropertyChanged(nameof(CanVote));
    OnPropertyChanged(nameof(CanCompose));
    OnPropertyChanged(nameof(CurrentUserId));
  }

  public void OnUiLocaleChanged() => RefreshRows();

  public void ToggleCollapse(string postId)
  {
    viewModel.ToggleCollapse(postId);
    RefreshRows();
  }

  public bool IsCommentCollapsed(string postId) => viewModel.CollapsedCommentIds.Contains(postId);

  private async Task LoadInternalAsync(string? focusedCommentId, CancellationToken cancellationToken)
  {
    await (focusedCommentId is null
        ? viewModel.LoadAsync(cancellationToken)
        : viewModel.LoadPermalinkAsync(focusedCommentId, cancellationToken)).ConfigureAwait(true);
    RefreshRows();
    await HnDiscussions.LoadAsync(viewModel.RootPost, cancellationToken).ConfigureAwait(true);
  }
}
