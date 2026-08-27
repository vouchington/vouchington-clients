namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<HouseholdsResponse> FetchHouseholdsAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdsResponse>(VouchaApiEndpoints.Households(), cancellationToken);

  public Task<HouseholdsResponse> FetchHouseholdsAsync(
      HouseholdAccess access,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdsResponse>(VouchaApiEndpoints.Households(access, after, limit), cancellationToken);

  public Task<HouseholdCreateResponse> CreateHouseholdAsync(CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdCreateResponse>(VouchaApiEndpoints.CreateHousehold(), cancellationToken);

  public Task<HouseholdMembershipsResponse> FetchHouseholdMembershipsAsync(
      string householdId,
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdMembershipsResponse>(
          VouchaApiEndpoints.HouseholdMemberships(householdId),
          cancellationToken);

  public Task<HouseholdMembershipsResponse> FetchHouseholdMembershipsAsync(
      string householdId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdMembershipsResponse>(
          VouchaApiEndpoints.HouseholdMemberships(householdId, after, limit),
          cancellationToken);

  public Task DeleteHouseholdMembershipAsync(
      string householdId,
      string membershipId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteHouseholdMembership(householdId, membershipId), cancellationToken);
}
