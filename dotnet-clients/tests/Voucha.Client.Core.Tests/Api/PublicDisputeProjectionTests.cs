using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PublicDisputeProjectionTests
{
  [Fact]
  public void MemberListKeepsCanonicalIdentityWithoutInventingPrivateValues()
  {
    var member = JsonSerializer.Deserialize<ModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.member.default"), VouchaApiJson.Options);
    var staff = JsonSerializer.Deserialize<ModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"), VouchaApiJson.Options);
    var memberDispute = Assert.Single(Assert.IsType<ModerationDisputeListResponse>(member).Disputes);
    var staffDispute = Assert.Single(Assert.IsType<ModerationDisputeListResponse>(staff).Disputes);

    Assert.Equal(staffDispute.Id, memberDispute.Id);
    Assert.Equal(staffDispute.PostId, memberDispute.PostId);
    Assert.Equal(staffDispute.Status, memberDispute.Status);
    Assert.Null(memberDispute.ClaimText);
    Assert.Null(memberDispute.DisputantUserId);
    Assert.Null(memberDispute.StaffContext);
    AssertNoProtectedFields(JsonSerializer.SerializeToElement(memberDispute, VouchaApiJson.Options));
  }

  [Fact]
  public void MemberDetailRoundTripsPublicValuesWhileStaffRetainsPrivateClaim()
  {
    var member = Assert.IsType<ModerationDisputeResponse>(JsonSerializer.Deserialize<ModerationDisputeResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.member.default"), VouchaApiJson.Options));
    var staff = Assert.IsType<ModerationDisputeResponse>(JsonSerializer.Deserialize<ModerationDisputeResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.default"), VouchaApiJson.Options));

    Assert.Equal(staff.Dispute.Id, member.Dispute.Id);
    Assert.Equal(staff.Dispute.PostContent?.Text, member.Dispute.PostContent?.Text);
    Assert.Null(member.Dispute.ClaimText);
    Assert.Null(member.Dispute.AiInternalResponse);
    AssertNoProtectedFields(JsonSerializer.SerializeToElement(member.Dispute, VouchaApiJson.Options));
    var encodedStaff = JsonSerializer.SerializeToElement(staff.Dispute, VouchaApiJson.Options);
    Assert.Equal("Native review dispute", encodedStaff.GetProperty("claim_text").GetString());
    Assert.Equal(staff.Dispute.DisputantUserId, encodedStaff.GetProperty("disputant_user_id").GetString());
  }

  [Theory]
  [InlineData("native.moderation.disputes.member.default", false)]
  [InlineData("native.moderation.disputes.member.default", true)]
  [InlineData("native.moderation.disputes.detail.member.default", false)]
  [InlineData("native.moderation.disputes.detail.member.default", true)]
  public void PublicRoundTripOmitsProtectedFieldsUnderBothNullPolicies(string fixtureId, bool includeNulls)
  {
    var source = JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixtureId))!;
    var original = source["dispute"] ?? source["disputes"]![0]!;
    var options = new JsonSerializerOptions(VouchaApiJson.Options)
    {
      DefaultIgnoreCondition = includeNulls
          ? System.Text.Json.Serialization.JsonIgnoreCondition.Never
          : System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
    var decoded = Assert.IsType<ModerationDispute>(JsonSerializer.Deserialize<ModerationDispute>(
        original, options));
    var encoded = JsonSerializer.SerializeToElement(decoded, options);

    AssertNoProtectedFields(encoded);
    Assert.Equal(original["id"]!.GetValue<string>(), encoded.GetProperty("id").GetString());
    var roundTrip = Assert.IsType<ModerationDispute>(JsonSerializer.Deserialize<ModerationDispute>(encoded, options));
    Assert.Equal(decoded.Id, roundTrip.Id);
    Assert.Equal(decoded.PostContent?.Text, roundTrip.PostContent?.Text);
  }

  [Fact]
  public void AnyProtectedFieldMarksStaffEvenWhenNull()
  {
    var response = JsonNode.Parse(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.member.default"))!;
    foreach (var field in ProtectedFields)
    {
      var dispute = response["dispute"]!.DeepClone().AsObject();
      dispute[field] = null;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<ModerationDispute>(dispute, VouchaApiJson.Options));
    }
  }

  [Fact]
  public void NoncanonicalProtectedSpellingCannotBindThroughMemberProjection()
  {
    var response = JsonNode.Parse(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.member.default"))!;
    foreach (var field in ProtectedFields)
    {
      var dispute = response["dispute"]!.DeepClone().AsObject();
      dispute[field.ToUpperInvariant()] = null;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<ModerationDispute>(dispute, VouchaApiJson.Options));
    }
  }

  private static readonly string[] ProtectedFields =
  [
    "disputant_user_id", "claim_text", "recommended_action", "ai_public_response",
    "ai_internal_response", "model", "ai_drafted_at", "internal_notes", "drafted_at",
    "edited_at", "edited_by_id", "approved_by_id", "resolved_by_id",
    "latest_lifecycle_change_id", "staff_context", "ai_draft_run_id", "ai_draft_requested_run_id",
  ];

  private static void AssertNoProtectedFields(JsonElement dispute)
  {
    foreach (var field in ProtectedFields)
    {
      Assert.False(dispute.TryGetProperty(field, out _), field);
    }
  }
}
