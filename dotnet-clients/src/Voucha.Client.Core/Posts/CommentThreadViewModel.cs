using Voucha.Client.Core.Api;
using Voucha.Client.Core.Contributions;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel : ObservableObject
{
  private readonly ICommentThreadService postsService;
  private readonly IPostsService? mutationPostsService;
  private readonly string rootPostId;
  private readonly string? currentUserId;
  private string sort = CommentThreadSorts.Best;
  private LoadState state = LoadState.Idle;
  private bool isLoadingAncestors;
  private bool isLoadingDescendants;
  private string? errorMessage;
  private string? focusedCommentId;
  private PostResponse? rootPostResponse;
  private PostThreadResponse? descendantsResponse;
  private PostThreadResponse? ancestorsResponse;
  private IReadOnlyList<Post> ancestorPosts = [];
  private IReadOnlyList<CommentThreadNodeViewModel> comments = [];
  private readonly HashSet<string> collapsedCommentIds = new(StringComparer.Ordinal);
  private Post? focusedComment;
  private readonly ContributionRequestIdentity contributionIdentity = new();
  private readonly IUiLocalization localization;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public CommentThreadViewModel(
      ICommentThreadService postsService,
      string rootPostId,
      string? currentUserId = null)
      : this(postsService, null, rootPostId, currentUserId)
  {
  }

  public CommentThreadViewModel(
      ICommentThreadService postsService,
      IPostsService? mutationPostsService,
      string rootPostId,
      string? currentUserId = null,
      IUiLocalization? localization = null)
  {
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.mutationPostsService = mutationPostsService;
    this.rootPostId = string.IsNullOrWhiteSpace(rootPostId)
        ? throw new ArgumentException("Root post id is required.", nameof(rootPostId))
        : rootPostId;
    this.currentUserId = string.IsNullOrWhiteSpace(currentUserId) ? null : currentUserId;
    this.localization = localization ?? UiLocalization.English;
  }

  public string RootPostId => rootPostId;

  public string? CurrentUserId => currentUserId;

  public string CollapseStateKey => BuildCollapseStateKey(rootPostId, currentUserId);

  public Post? RootPost => rootPostResponse?.Post;

  public string? RootHtml => rootPostResponse?.Html;

  public IReadOnlyList<Post> AncestorPosts
  {
    get => ancestorPosts;
    private set => SetProperty(ref ancestorPosts, value);
  }

  public IReadOnlyList<CommentThreadNodeViewModel> Comments
  {
    get => comments;
    private set => SetProperty(ref comments, value);
  }

  public IReadOnlySet<string> CollapsedCommentIds => collapsedCommentIds;

  public Post? FocusedComment
  {
    get => focusedComment;
    private set => SetProperty(ref focusedComment, value);
  }

  public string? FocusedCommentId
  {
    get => focusedCommentId;
    private set => SetProperty(ref focusedCommentId, value);
  }

  public string Sort
  {
    get => sort;
    set
    {
      var normalized = NormalizeSort(value);
      if (!SetProperty(ref sort, normalized)) return;
      RebuildComments();
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public bool IsLoadingAncestors
  {
    get => isLoadingAncestors;
    private set
    {
      if (SetProperty(ref isLoadingAncestors, value))
      {
        OnPropertyChanged(nameof(IsPermalinkLoading));
      }
    }
  }

  public bool IsLoadingDescendants
  {
    get => isLoadingDescendants;
    private set
    {
      if (SetProperty(ref isLoadingDescendants, value))
      {
        OnPropertyChanged(nameof(IsPermalinkLoading));
      }
    }
  }

  public bool IsPermalinkLoading => IsLoadingAncestors || IsLoadingDescendants && FocusedCommentId is not null;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      LoadInternalAsync(null, cancellationToken);

  public Task LoadPermalinkAsync(string commentId, CancellationToken cancellationToken = default) =>
      LoadInternalAsync(commentId, cancellationToken);

  public void ToggleCollapse(string postId)
  {
    if (!collapsedCommentIds.Remove(postId))
    {
      collapsedCommentIds.Add(postId);
    }

    OnPropertyChanged(nameof(CollapsedCommentIds));
  }
}
