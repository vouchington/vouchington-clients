using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Engineering;

public sealed record AgentListRow(
    string Id,
    string Title,
    string Detail,
    NativeRoutePath TargetPath,
    bool UsesLocalizedUntitledConversationTitle = false,
    string? UserId = null,
    DateTimeOffset? CreatedAt = null,
    bool IsAgentDirectoryRow = false,
    bool? IsActive = null,
    string? AgentType = null);

public sealed partial class AgentListsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IAgentConversationsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<AgentListRow> rows = [];
  private bool isLoading;
  private bool isLoadingMore;
  private bool hasMore;
  private string? errorMessage;
  private string? paginationErrorMessage;
  private UiText? localizedErrorText;
  private UiText? localizedPaginationErrorText;
  private string? agentIdOrSlug;
  private string? nextCursor;
  private AgentDetailResponse? agentDetail;
  private AgentConversationFilter? filter;
  private int selectedFilterKindIndex = (int)AgentConversationFilterKind.Username;
  private int contextGeneration;
  private int pageRequestGeneration;
  private readonly Dictionary<string, PublicUser> agentDirectoryUsers = [];
  private readonly Dictionary<string, PublicUser> agentConversationUsers = [];

  public AgentListsViewModel(
      IAgentConversationsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<AgentListRow> Rows
  {
    get => rows;
    private set { if (SetProperty(ref rows, value)) OnPropertyChanged(nameof(IsEmpty)); }
  }
  public AgentDetailResponse? AgentDetail
  {
    get => agentDetail;
    private set
    {
      if (SetProperty(ref agentDetail, value))
      {
        OnPropertyChanged(nameof(AgentDisplayName));
        OnPropertyChanged(nameof(AgentCreatedAt));
        OnPropertyChanged(nameof(AgentIsActive));
        OnPropertyChanged(nameof(AgentStatusTitle));
        OnPropertyChanged(nameof(HasAgentStatus));
      }
    }
  }
  public AgentConversationFilter? Filter { get => filter; private set => SetProperty(ref filter, value); }
  public bool IsDetail => agentIdOrSlug is not null;
  public IReadOnlyList<string> FilterLabels => Enum.GetValues<AgentConversationFilterKind>()
      .Select(FilterLabel).ToArray();
  public int SelectedFilterKindIndex
  {
    get => selectedFilterKindIndex;
    set
    {
      var next = Enum.IsDefined((AgentConversationFilterKind)value)
          ? value
          : (int)AgentConversationFilterKind.Username;
      if (SetProperty(ref selectedFilterKindIndex, next))
      {
        OnPropertyChanged(nameof(SelectedFilterKind));
        OnPropertyChanged(nameof(FilterValueAccessibilityLabel));
      }
    }
  }
  public AgentConversationFilterKind SelectedFilterKind => (AgentConversationFilterKind)SelectedFilterKindIndex;
  public string FilterKindAccessibilityLabel => localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceAgentFilterKind);
  public string FilterValueAccessibilityLabel => localization.Format(
      UiMessageKey.NativeSwiftRouteSurfaceAgentFilterValue,
      ("filter", FilterLabel(SelectedFilterKind)));

  public string AgentDisplayName => AgentDetail is { } detail
      ? FirstText(detail.User?.DisplayAccount?.Name, detail.User?.Name, detail.User?.Username, detail.Agent.SystemUserId)
      : string.Empty;
  public string AgentCreatedAt => AgentDetail is { } detail
      ? localization.FormatDateTime(detail.Agent.CreatedAt, TimeZoneInfo.Local)
      : string.Empty;

  public bool? AgentIsActive => AgentDetail is { } detail
      ? detail.Agent.ActivatedAt is not null && detail.Agent.DeactivatedAt is null
      : null;
  public bool HasAgentStatus => AgentIsActive is not null;
  public string? AgentStatusTitle => AgentIsActive is { } isActive
      ? localization.Localize(isActive ? UiMessageKey.NativeSwiftRouteSurfaceAgentStatusActive : UiMessageKey.NativeSwiftRouteSurfaceAgentStatusInactive)
      : null;

  public bool IsLoading
  {
    get => isLoading;
    private set { if (SetProperty(ref isLoading, value)) { OnPropertyChanged(nameof(CanLoadMore)); OnPropertyChanged(nameof(IsEmpty)); } }
  }

  public bool IsLoadingMore
  {
    get => isLoadingMore;
    private set { if (SetProperty(ref isLoadingMore, value)) OnPropertyChanged(nameof(CanLoadMore)); }
  }

  public bool HasMore
  {
    get => hasMore;
    private set { if (SetProperty(ref hasMore, value)) OnPropertyChanged(nameof(CanLoadMore)); }
  }

  public bool CanLoadMore => HasMore && !IsLoading && !IsLoadingMore;
  public bool IsEmpty => !IsLoading && ErrorMessage is null && Rows.Count == 0;
  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set
    {
      var hadLocalizedText = localizedErrorText is not null;
      localizedErrorText = null;
      if (hadLocalizedText || SetProperty(ref errorMessage, value)) { OnPropertyChanged(nameof(ErrorMessage)); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(IsEmpty)); }
    }
  }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public string? PaginationErrorMessage
  {
    get => localizedPaginationErrorText is { } text ? localization.Resolve(text) : paginationErrorMessage;
    private set
    {
      var hadLocalizedText = localizedPaginationErrorText is not null;
      localizedPaginationErrorText = null;
      if (hadLocalizedText || SetProperty(ref paginationErrorMessage, value))
      {
        OnPropertyChanged(nameof(PaginationErrorMessage));
        OnPropertyChanged(nameof(PaginationActionTitle));
        OnPropertyChanged(nameof(HasPaginationError));
      }
    }
  }

  public string PaginationActionTitle => localization.Localize(
      PaginationErrorMessage is null
          ? UiMessageKey.NativeSwiftCommonLoadMore
          : UiMessageKey.NativeDotnetEngineeringPaginationRetry);

  public bool HasPaginationError => !string.IsNullOrWhiteSpace(PaginationErrorMessage);

  private void SetLocalizedError(UiText? text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(IsEmpty));
  }

  private void SetLocalizedPaginationError(UiText? text)
  {
    localizedPaginationErrorText = text;
    paginationErrorMessage = null;
    OnPropertyChanged(nameof(PaginationErrorMessage));
    OnPropertyChanged(nameof(PaginationActionTitle));
    OnPropertyChanged(nameof(HasPaginationError));
  }

  public void Dispose() => localeSubscription?.Dispose();

  private string FilterLabel(AgentConversationFilterKind kind) => localization.Localize(kind switch
  {
    AgentConversationFilterKind.UserId => UiMessageKey.NativeSwiftRouteSurfaceAgentFilterUserId,
    AgentConversationFilterKind.Username => UiMessageKey.NativeSwiftRouteSurfaceAgentFilterUsername,
    AgentConversationFilterKind.PostId => UiMessageKey.NativeSwiftRouteSurfaceAgentFilterPostId,
    AgentConversationFilterKind.PostSlug => UiMessageKey.NativeSwiftRouteSurfaceAgentFilterPostSlug,
    _ => UiMessageKey.NativeSwiftRouteSurfaceAgentFilterRssFeedItemId,
  });
}
