using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

// Explicit web contract shape checks do not admit these fixtures to the native consumer registry.
public sealed class HouseholdInvitationWebContractShapeTests
{
  [Theory]
  [InlineData("sent")]
  [InlineData("received")]
  public void PublishedWebListsDecodeEmbeddedIdentitiesAndPagination(string action)
  {
    var response = Decode<HouseholdInvitationsResponse>(action);
    var invitation = Assert.Single(response.Results);

    Assert.Equal("00000000-0000-7000-8000-000000000101", invitation.HouseholdId);
    Assert.Equal("household-invitee", invitation.InvitedUser.Username);
    Assert.Equal(invitation.InvitedUserId, invitation.InvitedUser.Id);
    Assert.Equal("household-owner", invitation.InvitedBy.Username);
    Assert.Equal(invitation.InvitedById, invitation.InvitedBy.Id);
    Assert.Equal("household-owner", invitation.HouseholdOwner.Username);
    Assert.Equal("partner", invitation.Relationship);
    Assert.Null(invitation.AcceptedAt);
    Assert.Null(invitation.DeclinedAt);
    Assert.Null(invitation.RevokedAt);
    Assert.Equal(2026, invitation.CreatedAt.Year);
    Assert.Equal(invitation.CreatedAt, invitation.UpdatedAt);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.Null(response.PageInfo.EndCursor);
    Assert.NotNull(response.PageInfo.StartCursor);
  }

  [Fact]
  public void PublishedWebCreateDecodesInvitationEnvelope()
  {
    var invitation = Decode<HouseholdInvitationResponse>("create").Invitation;

    Assert.Equal("00000000-0000-7000-8000-000000000401", invitation.Id);
    Assert.Equal("household-invitee", invitation.InvitedUser.Username);
    Assert.Equal("household-owner", invitation.HouseholdOwner.Username);
    Assert.Equal("partner", invitation.Relationship);
  }

  [Fact]
  public void PublishedWebAcceptanceDecodesMembershipEnvelope()
  {
    var membership = Decode<HouseholdInvitationAcceptanceResponse>("accept").Membership;

    Assert.Equal("00000000-0000-7000-8000-000000000201", membership.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000101", membership.HouseholdId);
    Assert.Equal("00000000-0000-7000-8000-000000000301", membership.Individual.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000003", membership.Individual.UserId);
    Assert.Equal("household-invitee", membership.Individual.Username);
    Assert.Equal("partner", membership.Relationship);
    Assert.Equal(2026, membership.UpdatedAt.Year);
  }

  [Theory]
  [InlineData("accepted_at")]
  [InlineData("declined_at")]
  [InlineData("revoked_at")]
  [InlineData("relationship")]
  [InlineData("created_at")]
  [InlineData("updated_at")]
  [InlineData("id")]
  [InlineData("household_id")]
  [InlineData("invited_user_id")]
  [InlineData("invited_by_id")]
  [InlineData("invited_user")]
  [InlineData("invited_by")]
  [InlineData("household_owner")]
  public void MissingRequiredInvitationFieldIsRejected(string field)
  {
    var invitation = JsonNode.Parse(File.ReadAllText(ContractPaths.ApiFixture(
        "responses/web.household-invitations.create.json")))!["invitation"]!.AsObject();
    invitation.Remove(field);

    Assert.Throws<JsonException>(() => invitation.Deserialize<HouseholdInvitation>(VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("accepted_at")]
  [InlineData("declined_at")]
  [InlineData("revoked_at")]
  [InlineData("relationship")]
  public void ExplicitNullInvitationFieldsRemainPresentOnRoundTrip(string field)
  {
    var invitation = JsonNode.Parse(File.ReadAllText(ContractPaths.ApiFixture(
        "responses/web.household-invitations.create.json")))!["invitation"]!;
    invitation[field] = null;

    var decoded = invitation.Deserialize<HouseholdInvitation>(VouchaApiJson.Options)!;
    var serialized = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!.AsObject();

    Assert.True(serialized.ContainsKey(field));
    Assert.Null(serialized[field]);
  }

  [Theory]
  [InlineData("invited_user")]
  [InlineData("invited_by")]
  [InlineData("household_owner")]
  public void EmbeddedUsernameRequiresPresenceButPreservesExplicitNull(string field)
  {
    var invitation = JsonNode.Parse(File.ReadAllText(ContractPaths.ApiFixture(
        "responses/web.household-invitations.create.json")))!["invitation"]!;
    var user = invitation[field]!.AsObject();
    user["username"] = null;

    var decoded = user.Deserialize<HouseholdInvitationUser>(VouchaApiJson.Options)!;
    Assert.Null(decoded.Username);
    Assert.True(JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!.AsObject().ContainsKey("username"));

    user.Remove("username");
    Assert.Throws<JsonException>(() => user.Deserialize<HouseholdInvitationUser>(VouchaApiJson.Options));
  }

  private static T Decode<T>(string action) =>
      JsonSerializer.Deserialize<T>(File.ReadAllText(ContractPaths.ApiFixture(
          $"responses/web.household-invitations.{action}.json")), VouchaApiJson.Options) ??
      throw new InvalidOperationException($"The published {action} invitation contract decoded as null.");
}
