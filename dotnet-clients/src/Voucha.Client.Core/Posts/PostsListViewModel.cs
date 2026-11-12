using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
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
  private string? activeTimeRange;
  private bool? activeHackerNewsFilter;
  private bool browsing;
  private readonly HackerNewsReferenceGate? hackerNewsReferences;
  private int requestId;
  private readonly HashSet<string> votingIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> bookmarkingKeys = new(StringComparer.Ordinal);

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public string HackerNewsFilterLabel => localization.Localize(UiMessageKey.SharedHackerNewsFilterLabel);

  public PostsListViewModel(
      IPostsService postsService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      HackerNewsReferenceGate? hackerNewsReferences = null)
  {
    this.hackerNewsReferences = hackerNewsReferences;
    if (hackerNewsReferences is not null) hackerNewsReferences.Changed += OnHackerNewsReferencesChanged;
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose()
  {
    if (hackerNewsReferences is not null) hackerNewsReferences.Changed -= OnHackerNewsReferencesChanged;
    localeSubscription?.Dispose();
  }

  public void OnUiLocaleChanged()
  {
    Items = Items.ToArray();
    OnPropertyChanged(nameof(HackerNewsFilterLabel));
  }

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
    private set => SetPropertyAndNotify(ref state, value, [nameof(IsLoading), nameof(HasError)]);
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
    private set => SetPropertyAndNotify(ref errorMessage, value, nameof(HasError));
  }

  public async Task LoadFeedAsync(
      string feedType = "any",
      string? postTypes = null,
      string? timeRange = null,
      CancellationToken cancellationToken = default)
  {
    activeFeedType = feedType;
    activePostTypes = postTypes;
    activeTimeRange = timeRange;
    HackerNewsFilter = null;
    SetProperty(ref browsing, false, nameof(IsHackerNewsFilterVisible));
    endCursor = null;
    HasMore = true;
    var currentRequest = BeginLoad();
    await ClientLoadAttempt.RunAsync(
        async () =>
        {
          var response = await postsService
              .FetchFeedAsync(
                  new FetchPostsFeedRequest(Feed: feedType, PostTypes: postTypes, TimeRange: timeRange),
                  cancellationToken)
              .ConfigureAwait(true);
          CompleteLoad(currentRequest, response, append: false);
        },
        () => CompleteCanceled(currentRequest),
        message => CompleteError(currentRequest, message)).ConfigureAwait(true);
  }

  public async Task LoadBrowseAsync(
      string? postTypes = null,
      string? timeRange = null,
      bool? hasHackerNewsReferences = null,
      CancellationToken cancellationToken = default)
  {
    activePostTypes = postTypes;
    activeTimeRange = timeRange;
    var config = RefreshHackerNewsReferencesAsync(cancellationToken);
    // A requested filter is only sent once the backend flag is known to be on.
    if (hasHackerNewsReferences is not null) await config.ConfigureAwait(true);
    var filter = hackerNewsReferences?.ReferencesVisible == true ? hasHackerNewsReferences : null;
    HackerNewsFilter = filter;
    SetProperty(ref browsing, true, nameof(IsHackerNewsFilterVisible));
    endCursor = null;
    HasMore = true;
    var currentRequest = BeginLoad();
    await ClientLoadAttempt.RunAsync(
        async () =>
        {
          var response = await postsService
              .FetchPostsAsync(new FetchPostsRequest(PostTypes: postTypes, TimeRange: timeRange,
                  HasHackerNewsReferences: filter), cancellationToken)
              .ConfigureAwait(true);
          // The flag fetch runs alongside the page fetch; settle it so the filter is final with the list.
          await config.ConfigureAwait(true);
          CompleteLoad(currentRequest, response, append: false);
        },
        () => CompleteCanceled(currentRequest),
        message => CompleteError(currentRequest, message)).ConfigureAwait(true);
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
    // Continue only with has_next_page and an opaque cursor; a short page is terminal.
    HasMore = response.PageInfo.HasNextPage && !string.IsNullOrEmpty(response.PageInfo.EndCursor);
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
