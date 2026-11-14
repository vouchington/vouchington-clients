namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<HouseholdInvitationsResponse> FetchHouseholdInvitationsAsync(
      string householdId, string? after = null, int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdInvitationsResponse>(
          VouchaApiEndpoints.HouseholdInvitations(householdId, after, limit), cancellationToken);

  public Task<HouseholdInvitationsResponse> FetchReceivedHouseholdInvitationsAsync(
      string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdInvitationsResponse>(
          VouchaApiEndpoints.ReceivedHouseholdInvitations(after, limit), cancellationToken);

  public Task<HouseholdInvitationResponse> CreateHouseholdInvitationAsync(
      string householdId, string username, string? relationship = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdInvitationResponse>(
          VouchaApiEndpoints.CreateHouseholdInvitation(householdId, username, relationship), cancellationToken);

  public Task RevokeHouseholdInvitationAsync(
      string householdId, string invitationId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.RevokeHouseholdInvitation(householdId, invitationId), cancellationToken);

  public Task<HouseholdInvitationAcceptanceResponse> AcceptHouseholdInvitationAsync(
      string invitationId, CancellationToken cancellationToken = default) =>
      SendAsync<HouseholdInvitationAcceptanceResponse>(
          VouchaApiEndpoints.AcceptHouseholdInvitation(invitationId), cancellationToken);

  public Task DeclineHouseholdInvitationAsync(
      string invitationId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeclineHouseholdInvitation(invitationId), cancellationToken);
}
