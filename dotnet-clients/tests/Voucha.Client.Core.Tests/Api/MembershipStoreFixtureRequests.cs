using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class MembershipStoreFixtureRequests
{
  public static Dictionary<string, ApiRequest> AddTo(Dictionary<string, ApiRequest> registry)
  {
    registry["native.memberships.me.default"] = VouchaApiEndpoints.MembershipMe();
    return registry;
  }
}
