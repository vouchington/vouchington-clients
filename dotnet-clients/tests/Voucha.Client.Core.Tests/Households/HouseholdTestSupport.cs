using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;

namespace Voucha.Client.Core.Tests.Households;

internal sealed class FakeHouseholdService : IHouseholdService
{
  private Task<HouseholdsResponse>? pendingCombinedHouseholds;
  internal Queue<Func<Task<HouseholdsResponse>>> HouseholdResults { get; } = [];

  internal Dictionary<HouseholdAccess, Queue<Func<Task<HouseholdsResponse>>>> AccessHouseholdResults { get; } = [];

  internal Dictionary<string, Queue<Func<Task<HouseholdMembershipsResponse>>>> MembershipResults { get; } =
      new(StringComparer.Ordinal);

  internal Queue<Func<Task<Household>>> CreateResults { get; } = [];

  internal Dictionary<string, Queue<Func<Task>>> RemovalResults { get; } = new(StringComparer.Ordinal);

  internal List<string> MembershipCalls { get; } = [];

  internal List<(HouseholdAccess Access, string? After, int Limit)> HouseholdCalls { get; } = [];

  internal List<string> RemovalCalls { get; } = [];

  public Task<HouseholdsResponse> FetchHouseholdsAsync(CancellationToken cancellationToken = default) =>
      FetchHouseholdsPageAsync(HouseholdAccess.All, null, 25, cancellationToken);

  public async Task<HouseholdsResponse> FetchHouseholdsPageAsync(
      HouseholdAccess access,
      string? after,
      int limit,
      CancellationToken cancellationToken = default)
  {
    HouseholdCalls.Add((access, after, limit));
    if (AccessHouseholdResults.TryGetValue(access, out var pages) && pages.Count > 0)
    {
      return await pages.Dequeue()();
    }
    if (access == HouseholdAccess.Owned)
    {
      pendingCombinedHouseholds = HouseholdResults.Count > 0
          ? HouseholdResults.Dequeue()()
          : Task.FromResult(Page([]));
    }
    var combined = pendingCombinedHouseholds ?? (HouseholdResults.Count > 0
        ? HouseholdResults.Dequeue()()
        : Task.FromResult(Page([])));
    if (access == HouseholdAccess.Member) pendingCombinedHouseholds = null;
    var response = await combined;
    var results = access switch
    {
      HouseholdAccess.Owned => response.Results.Where(item => item.OwnerId == "me").Take(1).ToArray(),
      HouseholdAccess.Member => response.Results.Where(item => item.OwnerId != "me").ToArray(),
      _ => response.Results,
    };
    return response with { Results = results };
  }

  public Task<Household> CreateHouseholdAsync(CancellationToken cancellationToken = default) =>
      CreateResults.Dequeue()();

  public Task<HouseholdMembershipsResponse> FetchMembershipsAsync(
      string householdId,
      CancellationToken cancellationToken = default) =>
      FetchMembershipsPageAsync(householdId, null, 25, cancellationToken);

  public Task<HouseholdMembershipsResponse> FetchMembershipsPageAsync(
      string householdId,
      string? after,
      int limit,
      CancellationToken cancellationToken = default)
  {
    MembershipCalls.Add(householdId);
    return MembershipResults.TryGetValue(householdId, out var results) && results.Count > 0
        ? results.Dequeue()()
        : Task.FromResult(Members([]));
  }

  public Task RemoveMembershipAsync(
      string householdId,
      string membershipId,
      CancellationToken cancellationToken = default)
  {
    RemovalCalls.Add(membershipId);
    return RemovalResults.TryGetValue(membershipId, out var results) && results.Count > 0
        ? results.Dequeue()()
        : Task.CompletedTask;
  }

  internal static Household Household(string id, string ownerId) =>
      new(id, ownerId, DateTimeOffset.Parse("2026-07-02T00:00:00Z"));

  internal static HouseholdMembership Membership(
      string id,
      string householdId,
      string? username = null,
      string? relationship = null) =>
      new(
          id,
          householdId,
          new HouseholdIndividual(
              $"individual-{id}",
              null,
              username,
              DateTimeOffset.Parse("2026-07-02T00:00:00Z")),
          relationship,
          DateTimeOffset.Parse("2026-07-02T00:00:00Z"));

  internal static HouseholdsResponse Page(params Household[] households) =>
      new(households, new PageInfo(null, false, null));

  internal static HouseholdsResponse PageWithMore(string endCursor, params Household[] households) =>
      new(households, new PageInfo(endCursor, true, null));

  internal static HouseholdMembershipsResponse Members(params HouseholdMembership[] memberships) =>
      new(memberships, new PageInfo(null, false, null));

  internal static HouseholdMembershipsResponse MembersWithMore(
      string endCursor,
      params HouseholdMembership[] memberships) =>
      new(memberships, new PageInfo(endCursor, true, null));
}
