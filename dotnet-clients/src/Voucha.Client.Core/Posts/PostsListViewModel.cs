using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IPostsService postsService;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<PostRow> items = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? endCursor;
  private bool hasMore;
  private string activeFeedType = "any";
  private string? activePostTypes;
  private bool browsing;
  private int requestId;
  private readonly HashSet<string> votingIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> bookmarkingKeys = new(StringComparer.Ordinal);

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public PostsListViewModel(
      IPostsService postsService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged() => Items = Items.ToArray();

  public IReadOnlyList<PostRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value))
      {
        OnPropertyChanged(nameof(HasItems));
      }
    }
  }

  public bool HasItems => Items.Count > 0;

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

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }

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

  public async Task LoadFeedAsync(
      string feedType = "any",
      string? postTypes = null,
      CancellationToken cancellationToken = default)
  {
    activeFeedType = feedType;
    activePostTypes = postTypes;
    browsing = false;
    endCursor = null;
    HasMore = true;
    var currentRequest = BeginLoad();
    try
    {
      var response = await postsService
          .FetchFeedAsync(
              new FetchPostsFeedRequest(Feed: feedType, PostTypes: postTypes),
              cancellationToken)
          .ConfigureAwait(true);
      CompleteLoad(currentRequest, response, append: false);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  public async Task LoadBrowseAsync(
      string? postTypes = null,
      CancellationToken cancellationToken = default)
  {
    activePostTypes = postTypes;
    browsing = true;
    endCursor = null;
    HasMore = true;
    var currentRequest = BeginLoad();
    try
    {
      var response = await postsService
          .FetchPostsAsync(new FetchPostsRequest(PostTypes: postTypes), cancellationToken)
          .ConfigureAwait(true);
      CompleteLoad(currentRequest, response, append: false);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    State = LoadState.Loading;
    ErrorMessage = null;
    return currentRequest;
  }

  private void CompleteLoad(int currentRequest, PostsFeedResponse response, bool append)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    var rows = RowsFrom(response);
    Items = append
        ? Items.Concat(rows).DistinctBy(row => row.Id, StringComparer.Ordinal).ToArray()
        : rows;
    endCursor = response.PageInfo.EndCursor;
    HasMore = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
    State = LoadState.Loaded;
  }

  private void CompleteError(int currentRequest, string message)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    if (Items.Count == 0) Items = [];
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void CompleteCanceled(int currentRequest)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    State = LoadState.Idle;
  }

}
