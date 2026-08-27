using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel : ObservableObject
{
  private const int DetailsLimit = 3800;
  private readonly IMemberAppealsService service;
  private readonly NavigationViewer viewer;
  private readonly MemberAppealDraftStore drafts;
  private readonly Dictionary<ModerationAppealStatus, CursorState<ModerationAppeal>> appealStates =
      Enum.GetValues<ModerationAppealStatus>().ToDictionary(status => status, _ => new CursorState<ModerationAppeal>());
  private readonly CursorState<MemberWarningNotice> warnings = new();
  private readonly CursorState<PersonalCommunityBan> bans = new();
  private readonly CursorState<PersonalRemovedPost> removals = new();
  private MemberAppealTarget? activeTarget;
  private bool isSubmitting;
  private bool lastSubmissionWasDuplicate;
  private bool identityIsLoading;
  private bool identityHasError;
  private MemberAppealSubmissionOutcome submissionOutcome;

  public MemberAppealsViewModel(
      IMemberAppealsService service,
      NavigationViewer viewer,
      MemberAppealsRoute route,
      MemberAppealDraftStore? drafts = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
    Route = route;
    this.drafts = drafts ?? new MemberAppealDraftStore();
  }

  public MemberAppealsRoute Route { get; }
  public bool IsSignedIn => viewer.IsAuthenticated && viewer.IdentityId is not null;
  public IReadOnlyList<ModerationAppeal> Appeals =>
      appealStates.Values.SelectMany(state => state.Items).ToArray();
  public IReadOnlyList<MemberAppealTarget> WarningTargets =>
      warnings.Items.Where(item => item.RevokedAt is null).Select(item => MemberAppealTarget.Warning(
          item.Id, item.CommunitySlug, item.PublicMessage, item.CreatedAt)).ToArray();
  public IReadOnlyList<MemberAppealTarget> BanTargets =>
      bans.Items.Where(item => item.LiftedAt is null).Select(MemberAppealTarget.Ban).ToArray();
  public IReadOnlyList<MemberAppealTarget> RemovedPostTargets =>
      removals.Items.Select(MemberAppealTarget.Removal).ToArray();
  public MemberAppealTarget? SuspensionTarget { get; private set; }
  public IReadOnlyList<MemberAppealTarget> EligibleTargets =>
      !PendingAppealsAreReconciled
          ? []
          : WarningTargets.Concat(BanTargets).Concat(RemovedPostTargets)
              .Concat(SuspensionTarget is null ? [] : [SuspensionTarget])
              .Where(CanAppeal)
              .ToArray();
  public MemberAppealTarget? ActiveTarget
  {
    get => activeTarget;
    private set => SetProperty(ref activeTarget, value);
  }
  public MemberAppealDraft ActiveDraft =>
      ActiveTarget is null || viewer.IdentityId is null
          ? new()
          : drafts.Get(viewer.IdentityId, ActiveTarget);
  public bool IsSubmitting
  {
    get => isSubmitting;
    private set => SetProperty(ref isSubmitting, value);
  }
  public bool LastSubmissionWasDuplicate
  {
    get => lastSubmissionWasDuplicate;
    private set => SetProperty(ref lastSubmissionWasDuplicate, value);
  }
  public MemberAppealSubmissionOutcome SubmissionOutcome
  {
    get => submissionOutcome;
    private set
    {
      if (SetProperty(ref submissionOutcome, value))
        OnPropertyChanged(nameof(SubmissionMessageKey));
    }
  }
  public Localization.UiMessageKey? SubmissionMessageKey =>
      MemberAppealSubmissionMessages.MessageKey(SubmissionOutcome);
  public bool IsLoading =>
      RelevantAppealsLoading ||
      RelevantNoticesLoading ||
      identityIsLoading;
  public bool HasLoadError =>
      RelevantAppealsHaveError ||
      RelevantNoticesHaveError ||
      identityHasError;
  public MemberAppealPaginationState WarningPagination => warnings.Presentation;
  public MemberAppealPaginationState BanPagination => bans.Presentation;
  public MemberAppealPaginationState RemovedPostPagination => removals.Presentation;
  public bool HasMoreWarnings => warnings.HasMore;
  public bool HasMoreBans => bans.HasMore;
  public bool HasMoreRemovedPosts => removals.HasMore;
  public bool HasMoreAppeals(ModerationAppealStatus status) => appealStates[status].HasMore;
  public MemberAppealPaginationState AppealPagination(ModerationAppealStatus status) =>
      appealStates[status].Presentation;
  public bool HasAppealError(ModerationAppealStatus status) =>
      appealStates[status].HasError;

  public void BeginAppeal(MemberAppealTarget target)
  {
    ArgumentNullException.ThrowIfNull(target);
    if (!CanAppeal(target)) return;
    SubmissionOutcome = MemberAppealSubmissionOutcome.Idle;
    ActiveTarget = target;
  }

  public void CancelAppeal()
  {
    ActiveTarget = null;
    SubmissionOutcome = MemberAppealSubmissionOutcome.Idle;
  }

  public void SetReason(ModerationAppealReason? reason)
  {
    if (ActiveTarget is null || viewer.IdentityId is null) return;
    drafts.SetReason(viewer.IdentityId, ActiveTarget, reason);
    SubmissionOutcome = MemberAppealSubmissionOutcome.Idle;
    OnPropertyChanged(nameof(ActiveDraft));
  }

  public void SetDetails(string details)
  {
    if (ActiveTarget is null || viewer.IdentityId is null) return;
    drafts.SetDetails(viewer.IdentityId, ActiveTarget, details);
    SubmissionOutcome = MemberAppealSubmissionOutcome.Idle;
    OnPropertyChanged(nameof(ActiveDraft));
  }

  private bool CanAppeal(MemberAppealTarget target) =>
      PendingAppealsAreReconciled &&
      !appealStates[ModerationAppealStatus.Pending].Items.Any(
          appeal => Matches(appeal, target));

  private bool PendingAppealsAreReconciled
  {
    get
    {
      var pending = appealStates[ModerationAppealStatus.Pending];
      return pending.HasSuccessfulLoad &&
          !pending.HasMore &&
          !pending.IsLoading &&
          !pending.HasError;
    }
  }

  private sealed class CursorState<T>
  {
    public List<T> Items { get; } = [];
    public string? EndCursor { get; set; }
    public bool HasMore { get; set; }
    public bool IsLoading { get; set; }
    public bool HasError { get; set; }
    public bool HasSuccessfulLoad { get; set; }
    public bool CanRetry { get; set; }
    public string? RetryAfter { get; set; }
    public bool RetryReplace { get; set; }
    public MemberAppealPaginationState Presentation =>
        new(HasMore, IsLoading, HasError);
  }

  private bool RelevantNoticesLoading => Route switch
  {
    MemberAppealsRoute.Tracking =>
        warnings.IsLoading || bans.IsLoading || removals.IsLoading,
    MemberAppealsRoute.Warnings => warnings.IsLoading,
    MemberAppealsRoute.Bans => bans.IsLoading,
    MemberAppealsRoute.RemovedPosts => removals.IsLoading,
    _ => false,
  };

  private bool RelevantAppealsLoading =>
      Route == MemberAppealsRoute.Tracking
          ? appealStates.Values.Any(state => state.IsLoading)
          : appealStates[ModerationAppealStatus.Pending].IsLoading;

  private bool RelevantAppealsHaveError =>
      Route == MemberAppealsRoute.Tracking
          ? appealStates.Values.Any(state => state.HasError)
          : appealStates[ModerationAppealStatus.Pending].HasError;

  private bool RelevantNoticesHaveError => Route switch
  {
    MemberAppealsRoute.Tracking =>
        warnings.HasError || bans.HasError || removals.HasError,
    MemberAppealsRoute.Warnings => warnings.HasError,
    MemberAppealsRoute.Bans => bans.HasError,
    MemberAppealsRoute.RemovedPosts => removals.HasError,
    _ => false,
  };
}
