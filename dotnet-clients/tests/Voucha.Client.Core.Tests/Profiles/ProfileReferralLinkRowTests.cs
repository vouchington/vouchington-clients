using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ProfileReferralLinkRowTests
{
  [Fact]
  public void PublicFixtureRowFallsBackToProgramNameAndOpensReferralProgram()
  {
    var response = JsonSerializer.Deserialize<ReferralLinksResponse>(
        ApiFixtureLoader.LoadResponse("native.referral-links.user.public"),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    var row = ProfileReferralLinkRow.From(response.Results[0]);

    Assert.Equal("Test Card", row.Title);
    Assert.Equal("/referral-program/test-card", ProfileNavigationTargets.ReferralLink(row));
  }

  [Fact]
  public void OwnerRowPrefersLabelAndNullInputsAreRejected()
  {
    var link = new ReferralLink(
        "link-1", "user-1", "program-1", "url-1", "https://example.com", "My link", null, null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Program", "program");

    Assert.Equal("My link", ProfileReferralLinkRow.From(link).Title);
    Assert.Throws<ArgumentNullException>(() => ProfileReferralLinkRow.From(null!));
    Assert.Throws<ArgumentNullException>(() => ProfileNavigationTargets.ReferralLink(null!));
  }
}
