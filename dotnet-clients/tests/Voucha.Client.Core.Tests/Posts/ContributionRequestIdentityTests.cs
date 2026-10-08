using System.Text.Json.Nodes;
using Voucha.Client.Core.Contributions;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class ContributionRequestIdentityTests
{
  [Fact]
  public void NestedChallengeRefreshRetainsIdentityWhileNestedContentChangesRotateIt()
  {
    var body = JsonNode.Parse("""
        {"title":"Draft","data":{"cf_turnstile_response":"old","items":[{"text":"Keep","recaptcha_token":"old"}]}}
        """)!;
    var identity = new ContributionRequestIdentity();
    var first = identity.KeyFor("post", ContributionRequestIdentity.CanonicalIntent(body));

    body["data"]!["cf_turnstile_response"] = "refreshed";
    body["data"]!["items"]![0]!["recaptcha_token"] = "refreshed";
    Assert.Equal(first, identity.KeyFor("post", ContributionRequestIdentity.CanonicalIntent(body)));

    body["data"]!["items"]![0]!["text"] = "Edited";
    var edited = identity.KeyFor("post", ContributionRequestIdentity.CanonicalIntent(body));
    Assert.NotEqual(first, edited);
    Assert.Equal(edited, identity.KeyFor("post", ContributionRequestIdentity.CanonicalIntent(body)));
  }

  [Fact]
  public void NormalizationPreservesUnrelatedNestedValuesAndDoesNotMutateTheRequest()
  {
    var body = JsonNode.Parse("""
        {"data":{"hp_website":{"unexpected":true},"items":[{"hp_phone":17,"value":null},["text",false,3]],"cf_turnstile_response_suffix":"keep","recaptcha_token":null}}
        """)!;
    var original = body.ToJsonString();

    Assert.Equal(
        """{"data":{"items":[{"value":null},["text",false,3]],"cf_turnstile_response_suffix":"keep"}}""",
        ContributionRequestIdentity.CanonicalIntent(body));
    Assert.Equal(original, body.ToJsonString());
  }
}
