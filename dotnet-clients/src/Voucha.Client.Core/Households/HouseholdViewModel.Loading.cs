using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdViewModel
{
  private const int PageLimit = 25;

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    int request;
    HouseholdNotificationPlan notifications;
    lock (householdStateGate)
    {
      request = Interlocked.Increment(ref listRequest);
      hasConfirmedNoOwnedHousehold = false;
      ownedHousehold = null;
      memberHouseholdPagination.Reset();
      notifications = ApplyDisplayedHouseholdsLocked();
      state = LoadState.Loading;
      errorMessage = null;
      notifications.State = true;
      notifications.ErrorMessage = true;
    }
    PublishNotifications(notifications);
    var owned = LoadOwnedProbeAsync(request, cancellationToken);
    var members = LoadMoreMemberHouseholdsAsync(request, cancellationToken);
    await Task.WhenAll(owned, members).ConfigureAwait(true);
    notifications = new HouseholdNotificationPlan();
    lock (householdStateGate)
    {
      if (request != listRequest) return;
      state = errorMessage is not null
          ? LoadState.Error
          : hasConfirmedNoOwnedHousehold || ownedHousehold is not null || memberHouseholdPagination.HasLoadedPage
              ? LoadState.Loaded
              : LoadState.Idle;
      notifications.State = true;
    }
    PublishNotifications(notifications);
  }

  public Task LoadMoreMemberHouseholdsAsync(CancellationToken cancellationToken = default)
  {
    int request;
    lock (householdStateGate) request = listRequest;
    return LoadMoreMemberHouseholdsAsync(request, cancellationToken);
  }

  public Task RetryMembershipsAsync(string householdId, CancellationToken cancellationToken = default)
  {
    var section = Sections.FirstOrDefault(item => item.Id == householdId);
    if (section is not null && !section.HasError) section.ResetForReload();
    return LoadMembershipsAsync(householdId, cancellationToken);
  }

  public async Task LoadMembershipsAsync(string householdId, CancellationToken cancellationToken = default)
  {
    var section = Sections.FirstOrDefault(item => item.Id == householdId);
    var request = section?.BeginNextPage();
    if (section is null || request is null) return;
    try
    {
      var response = await service.FetchMembershipsPageAsync(
          householdId,
          request.Cursor,
          PageLimit,
          cancellationToken).ConfigureAwait(true);
      if (!Sections.Any(item => ReferenceEquals(item, section))) return;
      section.ApplyMembers(request, response.Results, response.PageInfo, PendingRemovalIds(householdId));
    }
    catch (OperationCanceledException)
    {
      section.CancelLoad(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      section.FailLoad(request, ex.Message);
    }
  }

  private async Task LoadOwnedProbeAsync(int request, CancellationToken cancellationToken)
  {
    try
    {
      var response = await service.FetchHouseholdsPageAsync(
          HouseholdAccess.Owned,
          after: null,
          limit: 1,
          cancellationToken: cancellationToken).ConfigureAwait(true);
      string? ownedHouseholdId;
      HouseholdNotificationPlan notifications;
      lock (householdStateGate)
      {
        if (request != listRequest) return;
        ownedHousehold = response.Results.Count == 0 ? null : response.Results[0];
        hasConfirmedNoOwnedHousehold = ownedHousehold is null;
        notifications = ApplyDisplayedHouseholdsLocked();
        ownedHouseholdId = ownedHousehold?.Id;
      }
      PublishNotifications(notifications);
      if (ownedHouseholdId is not null)
        await LoadMembershipsAsync(ownedHouseholdId, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      var notifications = new HouseholdNotificationPlan();
      lock (householdStateGate)
      {
        if (request != listRequest) return;
        errorMessage = ex.Message;
        notifications.ErrorMessage = true;
      }
      PublishNotifications(notifications);
    }
  }

  private async Task LoadMoreMemberHouseholdsAsync(int request, CancellationToken cancellationToken)
  {
    CursorPageRequest? pageRequest;
    var notifications = new HouseholdNotificationPlan();
    lock (householdStateGate)
    {
      if (request != listRequest) return;
      pageRequest = memberHouseholdPagination.BeginNextPage();
      if (pageRequest is null) return;
      notifications.MemberPagination = true;
    }
    PublishNotifications(notifications);
    try
    {
      var response = await service.FetchHouseholdsPageAsync(
          HouseholdAccess.Member,
          pageRequest.Cursor,
          PageLimit,
          cancellationToken).ConfigureAwait(true);
      BeforeMemberHouseholdApply?.Invoke();
      string[] newSectionIds;
      notifications = new HouseholdNotificationPlan();
      lock (householdStateGate)
      {
        if (request != listRequest || !memberHouseholdPagination.Complete(
            pageRequest,
            response.Results,
            response.PageInfo.EndCursor,
            response.PageInfo.HasNextPage)) return;
        var previousIds = Sections.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        notifications = ApplyDisplayedHouseholdsLocked();
        newSectionIds = MemberOnlyHouseholds
            .Where(section => !previousIds.Contains(section.Id))
            .Select(section => section.Id)
            .ToArray();
        notifications.MemberPagination = true;
      }
      PublishNotifications(notifications);
      await Task.WhenAll(newSectionIds.Select(id => LoadMembershipsAsync(id, cancellationToken))).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      notifications = new HouseholdNotificationPlan();
      lock (householdStateGate)
      {
        notifications.MemberPagination = memberHouseholdPagination.Cancel(pageRequest);
      }
      PublishNotifications(notifications);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      notifications = new HouseholdNotificationPlan();
      lock (householdStateGate)
      {
        notifications.MemberPagination = memberHouseholdPagination.Fail(pageRequest, ex.Message);
      }
      PublishNotifications(notifications);
    }
  }

}
