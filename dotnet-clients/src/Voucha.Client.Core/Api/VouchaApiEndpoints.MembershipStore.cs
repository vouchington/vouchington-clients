namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CreateMembershipPurchaseIntent(MembershipPurchaseIntentBody body) =>
      new(HttpMethod.Post, "/api/v1/membership-purchase-intents") { Body = body };

  public static ApiRequest CreateMembershipVerification(MembershipVerificationBody body) =>
      new(HttpMethod.Post, "/api/v1/membership-verifications") { Body = body };

  public static ApiRequest MembershipVerification(string verificationId) =>
      Get($"/api/v1/membership-verifications/{Path(verificationId)}");

  public static ApiRequest MicrosoftStoreServiceTickets() =>
      new(HttpMethod.Post, "/api/v1/memberships/microsoft-store/service-tickets")
      {
        Body = new EmptyJsonObjectBody()
      };

  public static ApiRequest CreateMembershipRefund(MembershipRefundBody body) =>
      new(HttpMethod.Post, "/api/v1/memberships/refunds") { Body = body };
}
