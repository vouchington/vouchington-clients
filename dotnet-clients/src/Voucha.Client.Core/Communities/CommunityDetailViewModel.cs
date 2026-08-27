using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly ICommunitiesService service;
  private readonly ISessionStore? sessionStore;
  private readonly IUiLocalization localization;
  private string communityIdOrSlug = "";
  private Community? community;
  private CommunityOwner? owner;
  private CommunityMember? membership;
  private CommunityMetrics? communityMetrics;
  private CommunityListItemCountsResponse? listItemCounts;
  private IReadOnlyList<CommunityMemberRow> members = [];
  private IReadOnlyList<CommunityPostRow> posts = [];
  private bool hasPendingApplication;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private readonly IDisposable? localeSubscription;

  public Community? Community
  {
    get => community;
    private set
    {
      if (SetProperty(ref community, value))
      {
        RaiseDerivedStateChanged();
      }
    }
  }

  public CommunityOwner? Owner
  {
    get => owner;
    private set => SetProperty(ref owner, value);
  }

  public CommunityMember? Membership
  {
    get => membership;
    private set
    {
      if (SetProperty(ref membership, value))
      {
        RaiseDerivedStateChanged();
      }
    }
  }

  public CommunityMetrics? CommunityMetrics
  {
    get => communityMetrics;
    private set => SetProperty(ref communityMetrics, value);
  }

  public CommunityListItemCountsResponse? ListItemCounts
  {
    get => listItemCounts;
    private set => SetProperty(ref listItemCounts, value);
  }

  public IReadOnlyList<CommunityMemberRow> Members
  {
    get => members;
    private set => SetProperty(ref members, value);
  }

  public IReadOnlyList<CommunityPostRow> Posts
  {
    get => posts;
    private set => SetProperty(ref posts, value);
  }

  public bool HasPendingApplication
  {
    get => hasPendingApplication;
    private set
    {
      if (SetProperty(ref hasPendingApplication, value))
      {
        RaiseDerivedStateChanged();
      }
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

  public bool IsAuthenticated => sessionStore?.Current.IsAuthenticated == true;

  public bool IsArchived => Community?.ArchivedAt is not null;

  public bool CanJoin => IsAuthenticated && Community is not null && !IsArchived && Membership is null && !HasPendingApplication;

  public bool CanLeave =>
      IsAuthenticated && Community is not null && !IsArchived && Membership is { Role: not "owner" };

  public bool CanArchive => CanManageCommunity && !IsArchived;

  public bool CanUnarchive => CanManageCommunity && IsArchived;

  public bool HasMembers => Members.Count > 0;

  public bool HasPosts => Posts.Count > 0;

  public async Task LoadAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(idOrSlug))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(idOrSlug));
    }

    communityIdOrSlug = idOrSlug;
    BeginLoad();
    try
    {
      ApplyDetail(await service.FetchDetailAsync(idOrSlug, cancellationToken).ConfigureAwait(true));
      if (Membership is null && (HasPendingApplication || (Community?.Visibility != "public" && !CanManageCommunity)))
      {
        CompleteLoad();
        return;
      }

      var membersTask = service.FetchMembersAsync(idOrSlug, cancellationToken);
      var postsTask = service.FetchPostsAsync(idOrSlug, cancellationToken);
      var countsTask = service.FetchListItemCountsAsync(idOrSlug, cancellationToken);
      await Task.WhenAll(membersTask, postsTask, countsTask).ConfigureAwait(true);

      var membersResponse = await membersTask.ConfigureAwait(true);
      var postsResponse = await postsTask.ConfigureAwait(true);
      ApplyMembers(membersResponse);
      ApplyPosts(postsResponse);
      ApplyInitialCommunityPagination(membersResponse, postsResponse);
      ListItemCounts = await countsTask.ConfigureAwait(true);
      CompleteLoad();
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  private void BeginLoad()
  {
    State = LoadState.Loading;
    ErrorMessage = null;
    Community = null;
    Owner = null;
    Membership = null;
    CommunityMetrics = null;
    ListItemCounts = null;
    Members = [];
    Posts = [];
    HasPendingApplication = false;
  }

  private void CompleteLoad() => State = LoadState.Loaded;
}
