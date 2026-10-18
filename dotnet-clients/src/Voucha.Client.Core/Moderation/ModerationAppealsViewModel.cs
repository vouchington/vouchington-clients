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
  private readonly IUiLocalization localization;
  private readonly NavigationViewer viewer;
  private readonly INavigationViewerProvider? viewerProvider;
  private readonly AppealAIRerunAdmissionStore admissionStore;
  private readonly AppealAIRerunAdmissionStore.OwnerKey? admissionOwner;
  private readonly IUiThreadDispatcher? uiDispatcher;
  private readonly SynchronizationContext? uiSynchronizationContext = SynchronizationContext.Current;
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
  private int accountRevision;
  private int activeMutationOwnerRevision;
  private Guid? activeMutationAdmissionToken;
  private int admissionObserverDisposed;

  public bool IsSignedIn { get; }
  public bool IsAdministrator { get; }
  public bool IsModerator { get; }
  public bool CanAccess => IsSignedIn && (IsAdministrator || IsModerator) &&
      (viewerProvider is null || viewer.HasSameIdentityAccess(viewerProvider.CurrentViewer));
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
  public bool CanRerun(ModerationAppeal appeal) { ArgumentNullException.ThrowIfNull(appeal); return admissionOwner is not null && CanMutate(appeal) && appeal.ApprovedAt is null && appeal.SentAt is null && Appeals.Any(item => item.Id == appeal.Id) && !IsAIRerunPending(appeal); }
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

  public void Dispose()
  {
    if (Interlocked.Exchange(ref admissionObserverDisposed, 1) != 0) return;
    admissionStore.Changed -= OnAdmissionChanged;
    localeSubscription?.Dispose();
    if (viewerProvider is not null) viewerProvider.ViewerChanged -= OnViewerChanged;
  }
}
