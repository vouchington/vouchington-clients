namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest PaymentCards(string? after = null, int limit = 25) =>
      Get("/api/v1/my/cards", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreatePaymentCard(CreatePaymentCardBody body) =>
      new(HttpMethod.Post, "/api/v1/my/cards") { Body = body ?? throw new ArgumentNullException(nameof(body)) };

  public static ApiRequest UpdatePaymentCard(string id, UpdatePaymentCardBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/cards/{Path(id)}")
      {
        Body = body ?? throw new ArgumentNullException(nameof(body)),
      };

  public static ApiRequest DeletePaymentCard(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/cards/{Path(id)}");

  public static ApiRequest PaymentCardTopics(string query, int limit = 10) =>
      SearchTopics(query, "card", limit);
}
