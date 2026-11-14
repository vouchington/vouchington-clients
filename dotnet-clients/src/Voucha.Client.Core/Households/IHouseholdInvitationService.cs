using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Households;

public interface IHouseholdInvitationService
{
  Task<HouseholdInvitationsResponse> FetchSentAsync(
      string householdId, string? after, int limit, CancellationToken cancellationToken = default);

  Task<HouseholdInvitationsResponse> FetchReceivedAsync(
      string? after, int limit, CancellationToken cancellationToken = default);

  Task<HouseholdInvitation> CreateAsync(
      string householdId, string username, string? relationship, CancellationToken cancellationToken = default);

  Task RevokeAsync(string householdId, string invitationId, CancellationToken cancellationToken = default);

  Task<HouseholdMembership> AcceptAsync(string invitationId, CancellationToken cancellationToken = default);

  Task DeclineAsync(string invitationId, CancellationToken cancellationToken = default);
}
