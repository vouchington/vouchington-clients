using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly IHouseholdService service;
  private readonly string currentUserId;
  private readonly IUiLocalization localization;
  private readonly object householdStateGate = new();
  private readonly IDisposable? localeSubscription;
  private readonly HashSet<string> pendingRemovalKeys = new(StringComparer.Ordinal);
  private readonly CursorPaginationState<Household, string> memberHouseholdPagination =
      new(household => household.Id);
  private IReadOnlyList<HouseholdSection> sections = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? mutationErrorMessage;
  private int listRequest;
  private bool isCreating;
  private bool hasConfirmedNoOwnedHousehold;
  private Household? ownedHousehold;

  internal Action? BeforeMemberHouseholdApply { get; set; }

  public HouseholdViewModel(
      IHouseholdService service,
      string currentUserId,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    ArgumentException.ThrowIfNullOrWhiteSpace(currentUserId);
    this.currentUserId = currentUserId;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public IReadOnlyList<HouseholdSection> Sections
  {
    get => sections;
    private set
    {
      sections = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(OwnedHousehold));
      OnPropertyChanged(nameof(MemberOnlyHouseholds));
      OnPropertyChanged(nameof(CanCreate));
    }
  }

  public HouseholdSection? OwnedHousehold => Sections.FirstOrDefault(section => section.IsOwned);

  public IReadOnlyList<HouseholdSection> MemberOnlyHouseholds =>
      Sections.Where(section => section.IsReadOnly).ToArray();

  public bool HasMoreMemberHouseholds
  {
    get { lock (householdStateGate) return memberHouseholdPagination.HasMore; }
  }

  public bool IsLoadingMoreMemberHouseholds
  {
    get { lock (householdStateGate) return memberHouseholdPagination.IsLoading; }
  }

  public bool HasMemberHouseholdError
  {
    get { lock (householdStateGate) return memberHouseholdPagination.LastError is not null; }
  }

  public string? ExternalContentMemberHouseholdError
  {
    get { lock (householdStateGate) return memberHouseholdPagination.LastError; }
  }

  public bool CanCreate =>
      hasConfirmedNoOwnedHousehold && OwnedHousehold is null && !IsCreating;

  public bool IsCreating
  {
    get => isCreating;
    private set
    {
      isCreating = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanCreate));
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      state = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
      OnPropertyChanged(nameof(CanCreate));
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public string? MutationErrorMessage
  {
    get => mutationErrorMessage;
    private set
    {
      mutationErrorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ExternalContentMutationError));
      OnPropertyChanged(nameof(HasMutationError));
    }
  }

  public bool HasMutationError => !string.IsNullOrWhiteSpace(MutationErrorMessage);

  public string? ExternalContentMutationError => MutationErrorMessage;

  public bool IsRemoving(string membershipId) =>
      pendingRemovalKeys.Any(key => key.EndsWith($"\0{membershipId}", StringComparison.Ordinal));

  public void OnUiLocaleChanged()
  {
    foreach (var section in Sections) section.OnUiLocaleChanged();
    OnPropertyChanged(nameof(Sections));
  }

  public void Dispose() => localeSubscription?.Dispose();

  private void PublishNotifications(HouseholdNotificationPlan notifications)
  {
    foreach (var section in notifications.RefreshedSections) section.PublishRefresh();
    if (notifications.Sections)
    {
      OnPropertyChanged(nameof(Sections));
      OnPropertyChanged(nameof(OwnedHousehold));
      OnPropertyChanged(nameof(MemberOnlyHouseholds));
      OnPropertyChanged(nameof(CanCreate));
    }
    if (notifications.State)
    {
      OnPropertyChanged(nameof(State));
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
      OnPropertyChanged(nameof(CanCreate));
    }
    if (notifications.ErrorMessage)
    {
      OnPropertyChanged(nameof(ErrorMessage));
      OnPropertyChanged(nameof(HasError));
    }
    if (notifications.MemberPagination) NotifyMemberPaginationChanged();
  }

  private sealed class HouseholdNotificationPlan
  {
    internal bool Sections { get; set; }
    internal bool State { get; set; }
    internal bool ErrorMessage { get; set; }
    internal bool MemberPagination { get; set; }
    internal List<HouseholdSection> RefreshedSections { get; } = [];
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
