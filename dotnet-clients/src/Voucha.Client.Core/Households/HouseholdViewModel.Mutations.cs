using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdViewModel
{
  public async Task CreateHouseholdAsync(CancellationToken cancellationToken = default)
  {
    if (!CanCreate) return;
    IsCreating = true;
    MutationErrorMessage = null;
    try
    {
      var household = await service.CreateHouseholdAsync(cancellationToken).ConfigureAwait(true);
      HouseholdNotificationPlan notifications;
      lock (householdStateGate)
      {
        if (OwnedHousehold is not null) return;
        hasConfirmedNoOwnedHousehold = false;
        Interlocked.Increment(ref listRequest);
        memberHouseholdPagination.InvalidateRequestsPreservingPage();
        ownedHousehold = household;
        notifications = ApplyDisplayedHouseholdsLocked();
        notifications.MemberPagination = true;
        if (state == LoadState.Loading)
        {
          state = LoadState.Loaded;
          notifications.State = true;
        }
      }
      PublishNotifications(notifications);
      await LoadMembershipsAsync(household.Id, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      MutationErrorMessage = null;
    }
    catch (VouchaApiException ex)
    {
      MutationErrorMessage = ex.Message;
    }
    catch (HttpRequestException ex)
    {
      MutationErrorMessage = ex.Message;
    }
    catch (InvalidOperationException ex)
    {
      MutationErrorMessage = ex.Message;
    }
    finally
    {
      IsCreating = false;
    }
  }

  public async Task RemoveMembershipAsync(
      HouseholdSection section,
      HouseholdMemberRow member,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(section);
    ArgumentNullException.ThrowIfNull(member);
    if (!section.IsOwned || !Sections.Any(item => ReferenceEquals(item, section))) return;
    var key = $"{section.Id}\0{member.Id}";
    if (!pendingRemovalKeys.Add(key)) return;
    section.CancelLoad();
    var capturedIndex = section.RemoveOptimistically(member.Id);
    if (capturedIndex < 0)
    {
      pendingRemovalKeys.Remove(key);
      return;
    }

    MutationErrorMessage = null;
    OnPropertyChanged(nameof(IsRemoving));
    try
    {
      await service.RemoveMembershipAsync(section.Id, member.Id, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackRemoval(section, member, capturedIndex, null);
    }
    catch (VouchaApiException ex)
    {
      RollbackRemoval(section, member, capturedIndex, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      RollbackRemoval(section, member, capturedIndex, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      RollbackRemoval(section, member, capturedIndex, ex.Message);
    }
    finally
    {
      section.CancelLoad();
      pendingRemovalKeys.Remove(key);
      OnPropertyChanged(nameof(IsRemoving));
    }
  }

  private void RollbackRemoval(
      HouseholdSection section,
      HouseholdMemberRow member,
      int capturedIndex,
      string? message)
  {
    if (Sections.Any(item => ReferenceEquals(item, section)))
    {
      section.Restore(member, capturedIndex);
      MutationErrorMessage = message;
    }
  }

  private HashSet<string> PendingRemovalIds(string householdId)
  {
    var prefix = $"{householdId}\0";
    return pendingRemovalKeys
        .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
        .Select(key => key[prefix.Length..])
        .ToHashSet(StringComparer.Ordinal);
  }
}
