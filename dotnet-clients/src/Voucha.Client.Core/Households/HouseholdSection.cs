using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdSection : INotifyPropertyChanged
{
  private readonly Dictionary<string, int> memberOrder = new(StringComparer.Ordinal);
  private readonly IUiLocalization localization;
  private readonly CursorPaginationState<HouseholdMembership, string> pagination =
      new(membership => membership.Id);
  private IReadOnlyList<HouseholdMemberRow> members = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;

  internal HouseholdSection(
      Household household,
      bool isOwned,
      IUiLocalization? localization = null)
  {
    Household = household;
    IsOwned = isOwned;
    this.localization = localization ?? UiLocalization.English;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public Household Household { get; private set; }

  public string Id => Household.Id;

  public string MemberPaginationId => $"household-{Id}-members";

  public bool IsOwned { get; private set; }

  public string LocalizedTitle => localization.Localize(IsOwned
      ? UiMessageKey.NativeSwiftHouseholdsBookmarksYourHousehold
      : UiMessageKey.NativeSwiftHouseholdsBookmarksHouseholdYouBelongTo);

  public bool IsReadOnly => !IsOwned;

  public IReadOnlyList<HouseholdMemberRow> Members
  {
    get => members;
    private set
    {
      members = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasMembers));
      OnPropertyChanged(nameof(ShowsEmptyState));
    }
  }

  public bool HasMembers => Members.Count > 0;
  public bool HasMoreMembers => pagination.HasMore;
  public bool CanAutomaticallyLoadMembers => pagination.CanAutomaticallyLoad;

  public LoadState State
  {
    get => state;
    private set
    {
      state = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
      OnPropertyChanged(nameof(ShowsEmptyState));
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
      OnPropertyChanged(nameof(ShowsEmptyState));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool ShowsEmptyState => !IsLoading && !HasError && !HasMembers;

  internal int MembershipRevision { get; private set; }

  internal void RefreshSilently(Household household, bool isOwned)
  {
    Household = household;
    IsOwned = isOwned;
  }

  internal void PublishRefresh()
  {
    OnPropertyChanged(nameof(Household));
    OnPropertyChanged(nameof(IsOwned));
    OnPropertyChanged(nameof(IsReadOnly));
    OnPropertyChanged(nameof(LocalizedTitle));
  }

  internal void BeginLoad()
  {
    State = LoadState.Loading;
    ErrorMessage = null;
  }

  internal CursorPageRequest? BeginNextPage()
  {
    var request = pagination.BeginNextPage();
    if (request is not null) BeginLoad();
    return request;
  }

  internal void ResetForReload()
  {
    pagination.Reset();
    OnPropertyChanged(nameof(HasMoreMembers));
    OnPropertyChanged(nameof(CanAutomaticallyLoadMembers));
  }

  internal bool ApplyMembers(
      CursorPageRequest request,
      IReadOnlyList<HouseholdMembership> memberships,
      PageInfo pageInfo,
      IReadOnlySet<string>? excludedMembershipIds = null)
  {
    if (!pagination.Complete(
        request,
        memberships,
        pageInfo.EndCursor,
        pageInfo.HasNextPage)) return false;
    Members = pagination.Items
        .Where(membership => excludedMembershipIds?.Contains(membership.Id) != true)
        .Select(membership => HouseholdMemberRow.FromMembership(membership, IsOwned, localization))
        .ToArray();
    MembershipRevision = unchecked(MembershipRevision + 1);
    memberOrder.Clear();
    for (var index = 0; index < pagination.Items.Count; index++) memberOrder[pagination.Items[index].Id] = index;
    State = LoadState.Loaded;
    ErrorMessage = null;
    OnPropertyChanged(nameof(HasMoreMembers));
    OnPropertyChanged(nameof(CanAutomaticallyLoadMembers));
    return true;
  }

  internal void FailLoad(string message)
  {
    State = LoadState.Error;
    ErrorMessage = message;
  }

  internal void FailLoad(CursorPageRequest request, string message)
  {
    if (!pagination.Fail(request, message)) return;
    FailLoad(message);
    OnPropertyChanged(nameof(CanAutomaticallyLoadMembers));
  }

  internal int RemoveOptimistically(string membershipId)
  {
    var index = Members.ToList().FindIndex(item => item.Id == membershipId);
    if (index < 0) return index;
    pagination.Remove(item => item.Id == membershipId);
    Members = Members.Where(item => item.Id != membershipId).ToArray();
    return index;
  }

  internal void Restore(HouseholdMemberRow row, int capturedIndex)
  {
    if (Members.Any(item => item.Id == row.Id)) return;
    var updated = Members.ToList();
    var index = memberOrder.TryGetValue(row.Id, out var rank)
        ? updated.FindIndex(item => memberOrder.GetValueOrDefault(item.Id, int.MaxValue) > rank)
        : -1;
    updated.Insert(index < 0 ? Math.Min(capturedIndex, updated.Count) : index, row);
    Members = updated;
    pagination.ReplaceItems(updated.Select(item => item.Membership));
  }

  internal void ClearError() => ErrorMessage = null;

  internal void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(LocalizedTitle));
    OnPropertyChanged(nameof(Members));
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
