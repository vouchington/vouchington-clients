using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CopyrightClaimantContractTests
{
  [Theory]
  [InlineData("web.copyright.notices.default", typeof(CopyrightNoticesResponse), true)]
  [InlineData("web.copyright.notices.null-claimant", typeof(CopyrightNoticesResponse), false)]
  [InlineData("web.copyright.notice.detail.populated", typeof(CopyrightNoticeResponse), true)]
  [InlineData("web.copyright.notice.detail.null-claimant", typeof(CopyrightNoticeResponse), false)]
  [InlineData("web.copyright.notice.participant.populated", typeof(CopyrightParticipantNoticeResponse), true)]
  [InlineData("web.copyright.notice.participant.null-claimant", typeof(CopyrightParticipantNoticeResponse), false)]
  public void CurrentPublicClaimantIsRequiredNullableAndRoundTrips(string fixtureId, Type responseType, bool populated)
  {
    var body = ApiFixtureLoader.LoadResponse(fixtureId);
    var decoded = JsonSerializer.Deserialize(body, responseType, VouchaApiJson.Options);
    var notice = decoded switch
    {
      CopyrightNoticesResponse list => Assert.Single(list.CopyrightNotices),
      CopyrightNoticeResponse detail => (CopyrightNoticeBase)detail.CopyrightNotice,
      CopyrightParticipantNoticeResponse participant => participant.CopyrightNotice,
      _ => throw new InvalidOperationException("Unexpected copyright response type."),
    };
    if (populated)
    {
      Assert.NotNull(notice.Claimant);
      Assert.Equal("Current claimant", notice.Claimant.DisplayName);
      Assert.Equal("00000000-0000-7000-8000-000000000806", notice.Claimant.UserId);
    }
    else
      Assert.Null(notice.Claimant);

    var encoded = JsonSerializer.SerializeToNode(decoded, responseType, VouchaApiJson.Options)!;
    var encodedNotice = Notice(encoded, responseType);
    Assert.True(encodedNotice.ContainsKey("claimant"));
    Assert.Equal(populated, encodedNotice["claimant"] is not null);

    var missing = JsonNode.Parse(body)!;
    Notice(missing, responseType).Remove("claimant");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(missing.ToJsonString(), responseType, VouchaApiJson.Options));
  }

  [Fact]
  public void CaseEndpointsEscapeIdsAndForwardOpaqueCursor()
  {
    Assert.Equal("/api/v1/copyright-notices/case%20%2F%20one", VouchaApiEndpoints.CopyrightNotice("case / one").Path);
    Assert.Equal("/api/v1/copyright-notices/case%20%2F%20one/participant", VouchaApiEndpoints.CopyrightParticipantNotice("case / one").Path);
    var request = VouchaApiEndpoints.CopyrightNotices("opaque/+==", 17);
    Assert.Equal("opaque/+==", request.Query!["after"]);
    Assert.Equal("17", request.Query["limit"]);
    Assert.Empty(VouchaApiEndpoints.CopyrightNotices().Query!);
  }

  private static JsonObject Notice(JsonNode root, Type responseType) =>
      (responseType == typeof(CopyrightNoticesResponse)
          ? root["copyright_notices"]![0]!
          : root["copyright_notice"]!).AsObject();
}
