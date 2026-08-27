using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Households;

public interface IHouseholdService
{
  Task<HouseholdsResponse> FetchHouseholdsAsync(CancellationToken cancellationToken = default);

  async Task<HouseholdsResponse> FetchHouseholdsPageAsync(
      HouseholdAccess access,
      string? after,
      int limit,
      CancellationToken cancellationToken = default)
  {
    var response = await FetchHouseholdsAsync(cancellationToken).ConfigureAwait(false);
    return response with { PageInfo = new PageInfo(null, false, null) };
  }

  Task<Household> CreateHouseholdAsync(CancellationToken cancellationToken = default);

  Task<HouseholdMembershipsResponse> FetchMembershipsAsync(
      string householdId,
      CancellationToken cancellationToken = default);

  async Task<HouseholdMembershipsResponse> FetchMembershipsPageAsync(
      string householdId,
      string? after,
      int limit,
      CancellationToken cancellationToken = default)
  {
    var response = await FetchMembershipsAsync(householdId, cancellationToken).ConfigureAwait(false);
    return response with { PageInfo = new PageInfo(null, false, null) };
  }

  Task RemoveMembershipAsync(
      string householdId,
      string membershipId,
      CancellationToken cancellationToken = default);
}
