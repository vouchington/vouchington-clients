using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithMembershipStoreEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    MembershipStoreFixtureRequests.AddTo(registry);
    var refund = new MembershipRefundBody(
        false,
        "ch_fixture_refund",
        "00000000-0000-7000-8000-000000000901",
        "in_fixture_refund",
        "goodwill",
        "019fafb8-a44c-73e2-890a-497ff3dd27a6");
    registry["web.memberships.refund.completed"] =
        VouchaApiEndpoints.CreateMembershipRefund(refund);
    registry["web.memberships.refund.reconciling"] =
        VouchaApiEndpoints.CreateMembershipRefund(refund);
    return registry;
  }
}
