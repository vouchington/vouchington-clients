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
  private FetchPostsRequest activeBrowseRequest = new();
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
    hackerNewsSubscription = hackerNewsReferences?.Subscribe(this);
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.localization = localization ?? UiLocalization.English;
    TimeRange = new(this.localization);
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose()
  {
    hackerNewsSubscription?.Dispose();
    localeSubscription?.Dispose();
  }

  public void OnUiLocaleChanged()
  {
    TimeRange.RefreshLabels();
    Items = Items.ToArray();
    OnPropertyChanged(nameof(HackerNewsFilterLabel));
  }

  public PostTimeRangeSelection TimeRange { get; }

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
      string? timeRange = PostTimeRanges.LastWeek,
      CancellationToken cancellationToken = default)
  {
    timeRange ??= PostTimeRanges.LastWeek;
    activeFeedType = feedType;
    activePostTypes = postTypes;
    activeTimeRange = timeRange;
    HackerNewsFilter = null;
    SetProperty(ref browsing, false, nameof(IsHackerNewsFilterVisible));
    endCursor = null;
    HasMore = true;
    var currentRequest = BeginLoad(NextRequestId());
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
      FetchPostsRequest? scope = null,
      CancellationToken cancellationToken = default)
  {
    timeRange = ConfigureBrowseTimeRange(timeRange, hasHackerNewsReferences);
    activePostTypes = postTypes;
    activeTimeRange = timeRange;
    // Claim the generation before any await so a newer selection always wins.
    var currentRequest = NextRequestId();
    var config = RefreshHackerNewsReferencesAsync(cancellationToken);
    // A requested filter is only sent once the backend flag is known to be on.
    if (hasHackerNewsReferences is not null) await HoldForHackerNewsConfigAsync(currentRequest, config).ConfigureAwait(true);
    if (currentRequest != Volatile.Read(ref requestId)) return;
    var filter = hackerNewsReferences?.ReferencesVisible == true ? hasHackerNewsReferences : null;
    HackerNewsFilter = filter;
    timeRange = ReconcileBrowseTimeRange(timeRange, hasHackerNewsReferences, filter);
    SetProperty(ref browsing, true, nameof(IsHackerNewsFilterVisible));
    activeBrowseRequest = (scope ?? new()) with { PostTypes = postTypes, TimeRange = timeRange, HasHackerNewsReferences = filter, After = null };
    var request = activeBrowseRequest;
    endCursor = null;
    HasMore = true;
    BeginLoad(currentRequest);
    await ClientLoadAttempt.RunAsync(
        async () =>
        {
          var response = await postsService
              .FetchPostsAsync(request, cancellationToken)
              .ConfigureAwait(true);
          CompleteLoad(currentRequest, response, append: false);
        },
        () => CompleteCanceled(currentRequest),
        message => CompleteError(currentRequest, message)).ConfigureAwait(true);
  }

  private int NextRequestId() => Interlocked.Increment(ref requestId);

  private int BeginLoad(int currentRequest)
  {
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
