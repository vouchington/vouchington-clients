using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly INewsFeedService newsFeedService;
  private readonly IBookmarkService bookmarkService;
  private readonly NewsFeedKind feedKind;
  private NewsFeedScope selectedScope;
  private IReadOnlyList<NewsFeedItem> items = [];
  private bool isLoading;
  private int loadRequestId;
  private string? errorMessage;
  private readonly HashSet<string> togglingSourceIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> togglingTopicIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> togglingArticleIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> votingArticleIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> startingStoryDiscussionStoryIds = new(StringComparer.Ordinal);
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public NewsFeedsViewModel(
      INewsFeedService newsFeedService,
      NewsFeedScope initialScope = NewsFeedScope.YourFeed,
      IBookmarkService? bookmarkService = null)
      : this(newsFeedService, NewsFeedKind.News, initialScope, bookmarkService)
  {
  }

  public NewsFeedsViewModel(INewsFeedService newsFeedService)
      : this(newsFeedService, NewsFeedKind.News, null)
  {
  }

  public NewsFeedsViewModel(INewsFeedService newsFeedService, NewsFeedKind feedKind)
      : this(newsFeedService, feedKind, null)
  {
  }

  public NewsFeedsViewModel(
      INewsFeedService newsFeedService,
      NewsFeedKind feedKind,
      NewsFeedScope? initialScope,
      IBookmarkService? bookmarkService = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.newsFeedService = newsFeedService;
    this.bookmarkService = bookmarkService ?? NullBookmarkService.Instance;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    this.feedKind = feedKind;
    selectedSourceFeedType = feedKind.GetDefaultSourceFeedType();
    selectedScope = initialScope ?? feedKind.GetAuthenticatedScope();
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public NewsFeedScope SelectedScope
  {
    get => selectedScope;
    private set
    {
      if (selectedScope == value) return;
      selectedScope = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsYourFeedSelected));
      OnPropertyChanged(nameof(IsAllNewsSelected));
      OnPropertyChanged(nameof(IsYourSourcesSelected));
      OnPropertyChanged(nameof(IsAllSourcesSelected));
      OnPropertyChanged(nameof(IsSourceScopeSelected));
    }
  }

  public bool IsYourFeedSelected => SelectedScope == feedKind.GetAuthenticatedScope();

  public bool IsAllNewsSelected => SelectedScope == feedKind.GetAnonymousScope();

  public bool IsYourSourcesSelected => SelectedScope == feedKind.GetSourcesScope();

  public bool IsAllSourcesSelected => SelectedScope == feedKind.GetAllSourcesScope();

  public NewsFeedKind FeedKind => feedKind;
  public string PageTitle => feedKind.GetPageTitle(localization);

  public string PrimaryScopeLabel => feedKind.GetPrimaryScopeLabel(localization);

  public string AllItemsScopeLabel => feedKind.GetAllItemsScopeLabel(localization);

  public string SourcesScopeLabel => feedKind.GetSourcesScopeLabel(localization);

  public string AllSourcesScopeLabel => feedKind.GetAllSourcesScopeLabel(localization);

  public IReadOnlyList<NewsFeedItem> Items
  {
    get => items;
    private set
    {
      if (ReferenceEquals(items, value)) return;
      items = value;
      feedPages.ReplaceItems(value);
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasItems));
    }
  }

  public bool HasItems => Items.Count > 0;

  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      if (isLoading == value) return;
      isLoading = value;
      OnPropertyChanged();
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (errorMessage == value) return;
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public async Task SelectScopeAsync(
      NewsFeedScope scope,
      CancellationToken cancellationToken = default)
  {
    SelectedScope = scope;
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "LoadAsync converts service load failures into state before callers such as MAUI async event handlers observe them.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
      => await LoadFeedPageAsync(replace: true, cancellationToken).ConfigureAwait(true);

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    foreach (var related in Items.Select(item => item.StoryArticles).OfType<StoryRelatedArticles>()) related.Notify();
    OnPropertyChanged(nameof(Items));
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
