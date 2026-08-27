using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly IFriendsService friendsService;
  private readonly Dictionary<FriendsTab, FriendsTabPage> tabCache = [];
  private readonly HashSet<string> followingIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, bool> localFollowStates = new(StringComparer.Ordinal);
  private readonly Dictionary<string, FriendRow> localFollowRows = new(StringComparer.Ordinal);
  private string? currentUserId;
  private FriendsTab selectedTab;
  private IReadOnlyList<FriendRow> items = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private bool hasMore;
  private int loadRequestId;
  private int mutationRequestId;
  private readonly HashSet<string> pendingFollowMutationIds = new(StringComparer.Ordinal);
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public FriendsViewModel(
      IFriendsService friendsService,
      string? currentUserId = null,
      FriendsTab initialTab = FriendsTab.Following,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.friendsService = friendsService ?? throw new ArgumentNullException(nameof(friendsService));
    this.currentUserId = string.IsNullOrWhiteSpace(currentUserId) ? null : currentUserId;
    selectedTab = initialTab;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(Items));

  public FriendsTab SelectedTab
  {
    get => selectedTab;
    private set
    {
      if (selectedTab == value) return;
      selectedTab = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsFollowingSelected));
      OnPropertyChanged(nameof(IsFollowersSelected));
    }
  }

  public bool IsFollowingSelected => SelectedTab == FriendsTab.Following;

  public bool IsFollowersSelected => SelectedTab == FriendsTab.Followers;

  public IReadOnlyList<FriendRow> Items
  {
    get => items;
    private set
    {
      if (ReferenceEquals(items, value)) return;
      items = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasItems));
    }
  }

  public bool HasItems => Items.Count > 0;

  public bool HasMore
  {
    get => hasMore;
    private set
    {
      if (hasMore == value) return;
      hasMore = value;
      OnPropertyChanged();
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (state == value) return;
      state = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool IsLoading => State == LoadState.Loading;

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

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      LoadSelectedTabAsync(forceReload: false, cancellationToken);

  public Task ReloadAsync(CancellationToken cancellationToken = default) =>
      LoadSelectedTabAsync(forceReload: true, cancellationToken);

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      LoadMoreSelectedTabAsync(cancellationToken);

  public async Task SelectTabAsync(FriendsTab tab, CancellationToken cancellationToken = default)
  {
    SelectedTab = tab;
    await LoadSelectedTabAsync(forceReload: false, cancellationToken).ConfigureAwait(true);
  }

  public Task ToggleFollowAsync(FriendRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    return ToggleFollowCoreAsync(row, cancellationToken);
  }

  public void UpdateCurrentUser(string? nextUserId)
  {
    var normalizedUserId = string.IsNullOrWhiteSpace(nextUserId) ? null : nextUserId;
    if (StringComparer.Ordinal.Equals(currentUserId, normalizedUserId)) return;

    currentUserId = normalizedUserId;
    loadRequestId = unchecked(loadRequestId + 1);
    mutationRequestId = unchecked(mutationRequestId + 1);
    ResetEmptyState();
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
