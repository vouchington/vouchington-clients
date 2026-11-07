using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityApplicantLabelTests
{
  [Fact]
  public void FixtureJoinsApplicantWithoutCommunityMembership()
  {
    var response = Read();
    var application = Assert.Single(response.CommunityApplications).Value;
    var row = CommunityApplicationRow.FromApplication(application, response.Users);
    Assert.Equal("user-1", row.UserId);
    Assert.Equal("testuser", UiLocalization.English.Resolve(row.ApplicantText));
    Assert.Equal("pending", row.Status);
    Assert.Equal("I would like to join.", row.Message);
    var named = response.Users.ToDictionary();
    named[application.UserId] = named[application.UserId] with { Name = "Applicant name" };
    Assert.Equal("Applicant name", UiLocalization.English.Resolve(CommunityApplicationRow.FromApplication(application, named).ApplicantText));
    var deleted = CommunityApplicationRow.FromApplication(application, new Dictionary<string, PublicUser>());
    Assert.Equal(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDeletedUser, deleted.ApplicantText.Key);
    Assert.Equal("Deleted user", UiLocalization.English.Resolve(deleted.ApplicantText));
  }

  [Fact]
  public void ApplicantsRequireUsersSidecar()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.communities.applications.default"))!;
    body.AsObject().Remove("users");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CommunityApplicationsResponse>(body.ToJsonString(), VouchaApiJson.Options));
  }

  private static CommunityApplicationsResponse Read() => JsonSerializer.Deserialize<CommunityApplicationsResponse>(
      ApiFixtureLoader.LoadResponse("web.communities.applications.default"), VouchaApiJson.Options)!;
}
