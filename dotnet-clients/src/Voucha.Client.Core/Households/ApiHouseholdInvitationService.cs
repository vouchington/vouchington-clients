using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Households;

public sealed class ApiHouseholdInvitationService : IHouseholdInvitationService
{
  private readonly VouchaApiClient client;

  public ApiHouseholdInvitationService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<HouseholdInvitationsResponse> FetchSentAsync(
      string householdId, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchHouseholdInvitationsAsync(householdId, after, limit, cancellationToken);

  public Task<HouseholdInvitationsResponse> FetchReceivedAsync(
      string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchReceivedHouseholdInvitationsAsync(after, limit, cancellationToken);

  public async Task<HouseholdInvitation> CreateAsync(
      string householdId, string username, string? relationship, CancellationToken cancellationToken = default) =>
      (await client.CreateHouseholdInvitationAsync(householdId, username, relationship, cancellationToken)
          .ConfigureAwait(false)).Invitation;

  public Task RevokeAsync(
      string householdId, string invitationId, CancellationToken cancellationToken = default) =>
      client.RevokeHouseholdInvitationAsync(householdId, invitationId, cancellationToken);

  public async Task<HouseholdMembership> AcceptAsync(
      string invitationId, CancellationToken cancellationToken = default) =>
      (await client.AcceptHouseholdInvitationAsync(invitationId, cancellationToken)
          .ConfigureAwait(false)).Membership;

  public Task DeclineAsync(string invitationId, CancellationToken cancellationToken = default) =>
      client.DeclineHouseholdInvitationAsync(invitationId, cancellationToken);
}
