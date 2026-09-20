using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class MembershipStoreFixtureRequests
{
  public static Dictionary<string, ApiRequest> AddTo(Dictionary<string, ApiRequest> registry)
  {
    registry["native.memberships.me.default"] = VouchaApiEndpoints.MembershipMe();
    registry["native.memberships.me.lifecycle.default"] = VouchaApiEndpoints.MembershipMe();
    registry["native.memberships.purchase-intent.apple.default"] =
        PurchaseIntent("00000000-0000-7000-8000-000000000803", "apple_app_store");
    registry["native.memberships.purchase-intent.google.default"] =
        PurchaseIntent("00000000-0000-7000-8000-000000000804", "google_play");
    registry["native.memberships.purchase-intent.microsoft.default"] =
        PurchaseIntent("00000000-0000-7000-8000-000000000805", "microsoft_store");
    registry["native.memberships.purchase-intent.stripe.default"] =
        PurchaseIntent("00000000-0000-7000-8000-000000000806", "stripe");
    registry["native.memberships.purchase-intent.conflict.default"] =
        PurchaseIntent("00000000-0000-7000-8000-000000000811", "apple_app_store");
    registry["native.memberships.verification.pending.default"] =
        VouchaApiEndpoints.CreateMembershipVerification(
            new MembershipVerificationBody(
                new MembershipVerificationEvidence("fixture-signed-transaction"),
                "00000000-0000-7000-8000-000000000807",
                "apple_app_store",
                "00000000-0000-7000-8000-000000000803"));
    registry["native.memberships.verification-status.pending.default"] =
        VouchaApiEndpoints.MembershipVerification("00000000-0000-7000-8000-000000000807");
    registry["native.memberships.verification-status.rejected.default"] =
        VouchaApiEndpoints.MembershipVerification("00000000-0000-7000-8000-000000000810");
    registry["native.memberships.verification-status.conflict.default"] =
        VouchaApiEndpoints.MembershipVerification("00000000-0000-7000-8000-000000000809");
    registry["native.memberships.verification-status.verified.default"] =
        VouchaApiEndpoints.MembershipVerification("00000000-0000-7000-8000-000000000808");
    registry["native.memberships.microsoft.service-tickets.default"] =
        VouchaApiEndpoints.MicrosoftStoreServiceTickets();
    return registry;
  }

  private static ApiRequest PurchaseIntent(string idempotencyKey, string provider) =>
      VouchaApiEndpoints.CreateMembershipPurchaseIntent(
          new MembershipPurchaseIntentBody(
              idempotencyKey,
              "00000000-0000-7000-8000-000000000701",
              provider));
}
