using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;

namespace Voucha.Client.Core.Tests.Households;

internal sealed class HouseholdInvitationsTestService : IHouseholdInvitationService
{
  internal List<(string? Household, string? Cursor, int Limit)> Requests { get; } = [];
  internal Func<string?, Task<HouseholdInvitationsResponse>> Fetch { get; set; } = _ => Task.FromResult(Page());
  internal Exception? Failure { get; set; }
  internal List<string> Actions { get; } = [];
  internal string? Username { get; private set; }
  internal string? Relationship { get; private set; }
  internal static HouseholdInvitation Invitation(string id = "invite", string? username = "invitee", string inviter = "owner") =>
      new(id, "home", "invitee-id", inviter, new("invitee-id", username), new(inviter, "inviter"),
          new("owner", "ownername"), "partner", null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
  internal static HouseholdInvitationsResponse Page(params HouseholdInvitation[] items) => new(items, new(null, false, null));
  public Task<HouseholdInvitationsResponse> FetchSentAsync(string householdId, string? after, int limit, CancellationToken cancellationToken = default)
  {
    Requests.Add((householdId, after, limit));
    return Fetch(after);
  }
  public Task<HouseholdInvitationsResponse> FetchReceivedAsync(string? after, int limit, CancellationToken cancellationToken = default)
  {
    Requests.Add((null, after, limit));
    return Fetch(after);
  }
  public Task<HouseholdInvitation> CreateAsync(string householdId, string username, string? relationship, CancellationToken cancellationToken = default)
  {
    Username = username;
    Relationship = relationship;
    Act("send");
    return Task.FromResult(Invitation());
  }
  public Task RevokeAsync(string householdId, string invitationId, CancellationToken cancellationToken = default) { Act("revoke"); return Task.CompletedTask; }
  public Task<HouseholdMembership> AcceptAsync(string invitationId, CancellationToken cancellationToken = default)
  {
    Act("accept");
    return Task.FromResult<HouseholdMembership>(null!);
  }
  public Task DeclineAsync(string invitationId, CancellationToken cancellationToken = default) { Act("decline"); return Task.CompletedTask; }
  private void Act(string action) { Actions.Add(action); if (Failure is not null) throw Failure; }
}
