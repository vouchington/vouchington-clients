using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IModerationAppealsService service;
  private readonly Func<CancellationToken, Task> rerunPollDelay;
  private readonly int rerunPollAttempts;
  private readonly IDisposable? localeSubscription;
  private readonly Dictionary<string, string> drafts = new(StringComparer.Ordinal);
  private readonly HashSet<string> dirtyDraftIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> ambiguousDeliveryIds = new(StringComparer.Ordinal);
  private IReadOnlyList<ModerationAppeal> appeals = [];
  private ModerationAppealStatus selectedStatus = ModerationAppealStatus.Pending;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? endCursor;
  private bool hasMore;
  private string? mutatingAppealId;
  private int loadRevision;

  public ModerationAppealsViewModel(
      IModerationAppealsService service,
      NavigationViewer viewer,
      IUiLocaleController? localeController = null)
      : this(
          service,
          viewer,
          20,
          token => Task.Delay(TimeSpan.FromSeconds(3), token),
          localeController)
  {
  }

  internal ModerationAppealsViewModel(
      IModerationAppealsService service,
      NavigationViewer viewer,
      int rerunPollAttempts,
      Func<CancellationToken, Task> rerunPollDelay,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    ArgumentNullException.ThrowIfNull(viewer);
    IsSignedIn = viewer.IsAuthenticated;
    IsAdministrator = viewer.Roles.Contains("administrator", StringComparer.Ordinal);
    IsModerator = viewer.Roles.Contains("moderator", StringComparer.Ordinal);
    this.rerunPollAttempts = rerunPollAttempts;
    this.rerunPollDelay = rerunPollDelay;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsSignedIn { get; }
  public bool IsAdministrator { get; }
  public bool IsModerator { get; }
  public bool CanAccess => IsSignedIn && (IsAdministrator || IsModerator);
  public bool IsMutating => MutatingAppealId is not null;
  public bool IsLoading => State == LoadState.Loading;
  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool HasAppeals => Appeals.Count > 0;
  public bool IsEmpty => State == LoadState.Loaded && !HasAppeals;

  public IReadOnlyList<ModerationAppeal> Appeals
  {
    get => appeals;
    private set
    {
      if (SetProperty(ref appeals, value))
      {
        OnPropertyChanged(nameof(HasAppeals));
        OnPropertyChanged(nameof(IsEmpty));
      }
    }
  }

  public ModerationAppealStatus SelectedStatus
  {
    get => selectedStatus;
    private set => SetProperty(ref selectedStatus, value);
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
        OnPropertyChanged(nameof(IsEmpty));
      }
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }

  public string? MutatingAppealId
  {
    get => mutatingAppealId;
    private set
    {
      if (SetProperty(ref mutatingAppealId, value)) OnPropertyChanged(nameof(IsMutating));
    }
  }

  public string DraftFor(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return drafts.TryGetValue(appeal.Id, out var value) ? value : appeal.PublicResponse ?? appeal.AiPublicResponse ?? string.Empty;
  }

  public void SetDraft(ModerationAppeal appeal, string value)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    var draft = value ?? string.Empty;
    drafts[appeal.Id] = draft;
    if (string.Equals(draft, ServerDraftFor(appeal), StringComparison.Ordinal))
    {
      dirtyDraftIds.Remove(appeal.Id);
    }
    else
    {
      dirtyDraftIds.Add(appeal.Id);
    }
  }

  public bool IsDeliveryAmbiguous(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return ambiguousDeliveryIds.Contains(appeal.Id);
  }

  public bool CanEdit(ModerationAppeal appeal) { ArgumentNullException.ThrowIfNull(appeal); return CanMutate(appeal) && appeal.SentAt is null; }
  public bool CanApprove(ModerationAppeal appeal) { ArgumentNullException.ThrowIfNull(appeal); return CanMutate(appeal) && appeal.ApprovedAt is null && appeal.SentAt is null && !string.IsNullOrWhiteSpace(DraftFor(appeal)); }
  public bool CanDeliver(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return CanMutate(appeal) &&
        appeal.ApprovedAt is not null &&
        appeal.SentAt is null &&
        !IsDeliveryAmbiguous(appeal) &&
        string.Equals(DraftFor(appeal), appeal.PublicResponse ?? string.Empty, StringComparison.Ordinal);
  }
  public bool CanRerun(ModerationAppeal appeal) { ArgumentNullException.ThrowIfNull(appeal); return CanMutate(appeal) && appeal.ApprovedAt is null && appeal.SentAt is null; }
  public bool CanResolve(ModerationAppeal appeal, ModerationAppealAction action)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return CanMutate(appeal) &&
        appeal.SentAt is not null &&
        (action != ModerationAppealAction.Accept || IsAdministrator || appeal.UserSuspensionId is null);
  }

  private bool CanMutate(ModerationAppeal appeal) => CanAccess && appeal.Status == ModerationAppealStatus.Pending && !IsMutating;

  private static string ServerDraftFor(ModerationAppeal appeal) =>
      appeal.PublicResponse ?? appeal.AiPublicResponse ?? string.Empty;

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(Appeals));

  public void Dispose() => localeSubscription?.Dispose();
}
