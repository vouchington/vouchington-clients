using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithMembershipGrantAndAncestorEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.comments.ancestors.bounded.shallow"] =
        VouchaApiEndpoints.PostAncestors("comment-b", limit: 5);
    registry["native.comments.ancestors.bounded.deep-initial"] =
        VouchaApiEndpoints.PostAncestors("bounded-ancestor-comment-7", limit: 5);
    registry["native.comments.ancestors.bounded.deep-continuation"] =
        VouchaApiEndpoints.PostAncestors(
            "bounded-ancestor-comment-7",
            "fixture-ancestor-deep-initial-end",
            5);
    registry["native.memberships.grant.delete.default"] =
        VouchaApiEndpoints.RevokeMembershipGrant(
            "00000000-0000-7000-8000-000000000802",
            new RevokeMembershipGrantBody("Incorrect grant"));
    return registry;
  }
}
