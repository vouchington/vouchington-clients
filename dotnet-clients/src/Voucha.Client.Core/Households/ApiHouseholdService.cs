using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Households;

public sealed class ApiHouseholdService : IHouseholdService
{
  private readonly VouchaApiClient client;

  public ApiHouseholdService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<HouseholdsResponse> FetchHouseholdsAsync(CancellationToken cancellationToken = default) =>
      client.FetchHouseholdsAsync(cancellationToken);

  public Task<HouseholdsResponse> FetchHouseholdsPageAsync(
      HouseholdAccess access,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchHouseholdsAsync(access, after, limit, cancellationToken);

  public async Task<Household> CreateHouseholdAsync(CancellationToken cancellationToken = default) =>
      (await client.CreateHouseholdAsync(cancellationToken).ConfigureAwait(false)).Household;

  public Task<HouseholdMembershipsResponse> FetchMembershipsAsync(
      string householdId,
      CancellationToken cancellationToken = default) =>
      client.FetchHouseholdMembershipsAsync(householdId, cancellationToken);

  public Task<HouseholdMembershipsResponse> FetchMembershipsPageAsync(
      string householdId,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchHouseholdMembershipsAsync(householdId, after, limit, cancellationToken);

  public Task RemoveMembershipAsync(
      string householdId,
      string membershipId,
      CancellationToken cancellationToken = default) =>
      client.DeleteHouseholdMembershipAsync(householdId, membershipId, cancellationToken);
}
