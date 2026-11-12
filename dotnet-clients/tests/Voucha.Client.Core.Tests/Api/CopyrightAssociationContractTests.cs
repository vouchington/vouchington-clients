using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CopyrightAssociationContractTests
{
  [Fact]
  public void AssociationSearchPreservesNullableClaimantAndEmptyContinuation()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.copyright.email-notice-association-search"))!;
    var notice = body["copyright_notices"]![0]!.AsObject();
    notice["claimant_display_name"] = null;
    var decoded = JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options)!;
    Assert.Null(Assert.Single(decoded.CopyrightNotices).ClaimantDisplayName);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!;
    Assert.True(encoded["copyright_notices"]![0]!.AsObject().ContainsKey("claimant_display_name"));
    notice.Remove("claimant_display_name");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options));

    body["copyright_notices"] = new JsonArray();
    body["page_info"]!["has_next_page"] = true;
    body["page_info"]!["end_cursor"] = "opaque/+==";
    decoded = JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options)!;
    Assert.Empty(decoded.CopyrightNotices);
    Assert.True(decoded.PageInfo.HasNextPage);
    var request = VouchaApiEndpoints.CopyrightAssociationTargets("Harbour photograph", decoded.PageInfo.EndCursor);
    Assert.Equal("/api/v1/copyright-notices/association-targets", request.Path);
    Assert.Equal("Harbour photograph", request.Query!["q"]);
    Assert.Equal("opaque/+==", request.Query["after"]);
    Assert.False(request.Query.ContainsKey("limit"));
  }

  [Theory]
  [InlineData("id")]
  [InlineData("jurisdiction")]
  [InlineData("received_at")]
  [InlineData("work_description")]
  public void AssociationIdentificationFieldsCannotBeMissing(string key)
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.copyright.email-notice-association-search"))!;
    body["copyright_notices"]![0]!.AsObject().Remove(key);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CopyrightAssociationTargetsResponse>(body, VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("image_id")]
  [InlineData("placement_id")]
  [InlineData("placement_revision")]
  public void PlacementTupleCannotBePartial(string key)
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.copyright.staff-queue.default"))!;
    body["copyright_notices"]![1]!["staydown_matches"]![0]!["image_placement"]!.AsObject().Remove(key);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CopyrightStaffQueueResponse>(body, VouchaApiJson.Options));
  }

  [Fact]
  public void ErasedAccountUsernameRemainsAnExplicitNull()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.copyright.repeat-infringer-accounts"))!;
    var account = body["copyright_repeat_infringer_accounts"]![0]!.AsObject();
    account["account_username"] = null;
    var decoded = JsonSerializer.Deserialize<CopyrightRepeatInfringerAccountsResponse>(body, VouchaApiJson.Options)!;
    Assert.Null(Assert.Single(decoded.Accounts).AccountUsername);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!;
    Assert.True(encoded["copyright_repeat_infringer_accounts"]![0]!.AsObject().ContainsKey("account_username"));
    account.Remove("account_username");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CopyrightRepeatInfringerAccountsResponse>(body, VouchaApiJson.Options));
  }

  [Fact]
  public void StaffNullableFactsRoundTripAndTargetIdentityIsRequired()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.copyright.staff-queue.default"))!;
    var decoded = JsonSerializer.Deserialize<CopyrightStaffQueueResponse>(body, VouchaApiJson.Options)!;
    var notice = decoded.CopyrightNotices[1];
    var available = notice.StaydownMatches[0];
    Assert.Equal("photo-author", available.UploadedByUsername);
    var placement = Assert.IsType<CopyrightStaffImagePlacement>(available.ImagePlacement);
    Assert.Equal(available.ImageId, placement.ImageId);
    Assert.Equal("00000000-0000-7000-8000-000000000822", placement.PlacementId);
    Assert.Equal(1, placement.PlacementRevision);
    var target = Assert.Single(notice.Targets, target => target.Id == notice.StaydownMatches[1].TargetId);
    Assert.Equal(target.Id, Assert.Single(notice.ActionIntents).TargetId);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!;
    var match = encoded["copyright_notices"]![1]!["staydown_matches"]![1]!.AsObject();
    Assert.True(match.ContainsKey("uploaded_by_username"));
    Assert.Null(match["uploaded_by_username"]);
    Assert.True(match.ContainsKey("image_placement"));
    Assert.Null(match["image_placement"]);
  }

  [Theory]
  [InlineData("action_intents", 0, "target_id")]
  [InlineData("staydown_matches", 0, "target_id")]
  [InlineData("staydown_matches", 1, "uploaded_by_username")]
  [InlineData("staydown_matches", 1, "image_placement")]
  public void StaffIdentityAndNullableFactsCannotBeMissing(string collection, int index, string key)
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.copyright.staff-queue.default"))!;
    body["copyright_notices"]![1]![collection]![index]!.AsObject().Remove(key);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CopyrightStaffQueueResponse>(body, VouchaApiJson.Options));
  }
}
