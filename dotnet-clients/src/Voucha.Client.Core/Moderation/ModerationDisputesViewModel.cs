using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IModerationDisputesService service;
  private readonly IUiLocalization localization;
  private readonly Func<CancellationToken, Task> rerunPollDelay;
  private readonly int rerunPollAttempts;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<ModerationDispute> disputes = [];
  private ModerationDisputeStatus selectedStatus = ModerationDisputeStatus.Pending;
  private LoadState state = LoadState.Idle;
  private UiMessageKey? errorMessageKey;
  private string? endCursor;
  private bool hasMore;
  private string? mutatingDisputeId;
  private Exception? lastMutationError;
  private int loadRevision;
  private int mutationOutcomeRevision;

  public ModerationDisputesViewModel(
      IModerationDisputesService service,
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

  internal ModerationDisputesViewModel(
      IModerationDisputesService service,
      NavigationViewer viewer,
      int rerunPollAttempts,
      Func<CancellationToken, Task> rerunPollDelay,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    ArgumentNullException.ThrowIfNull(viewer);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rerunPollAttempts);
    IsSignedIn = viewer.IsAuthenticated;
    IsAdministrator = viewer.Roles.Contains("administrator", StringComparer.Ordinal);
    IsModerator = viewer.Roles.Contains("moderator", StringComparer.Ordinal);
    this.rerunPollAttempts = rerunPollAttempts;
    this.rerunPollDelay = rerunPollDelay ?? throw new ArgumentNullException(nameof(rerunPollDelay));
    localization = localeController is null
        ? UiLocalization.English
        : new UiLocalization(localeController);
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsSignedIn { get; }
  public bool IsAdministrator { get; }
  public bool IsModerator { get; }
  public bool CanAccess => IsSignedIn && (IsAdministrator || IsModerator);
  public bool IsMutating => MutatingDisputeId is not null;
  public bool IsLoading => State == LoadState.Loading;
  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool HasDisputes => Disputes.Count > 0;
  public bool IsEmpty => State == LoadState.Loaded && !HasDisputes;

  public IReadOnlyList<ModerationDispute> Disputes
  {
    get => disputes;
    private set
    {
      if (!SetProperty(ref disputes, value)) return;
      OnPropertyChanged(nameof(HasDisputes));
      OnPropertyChanged(nameof(IsEmpty));
    }
  }

  public ModerationDisputeStatus SelectedStatus
  {
    get => selectedStatus;
    private set => SetProperty(ref selectedStatus, value);
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (!SetProperty(ref state, value)) return;
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
      OnPropertyChanged(nameof(IsEmpty));
    }
  }

  public string? ErrorMessage =>
      errorMessageKey is { } key ? localization.Localize(key) : null;

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }

  public string? MutatingDisputeId
  {
    get => mutatingDisputeId;
    private set
    {
      if (!SetProperty(ref mutatingDisputeId, value)) return;
      OnPropertyChanged(nameof(IsMutating));
      OnPropertyChanged(nameof(Disputes));
    }
  }

  public Exception? LastMutationError
  {
    get => lastMutationError;
    private set => SetProperty(ref lastMutationError, value);
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(Disputes));
  }

  private void SetError(UiMessageKey? key)
  {
    if (errorMessageKey == key) return;
    errorMessageKey = key;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }

  public void Dispose() => localeSubscription?.Dispose();
}
