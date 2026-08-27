namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PaymentCardsResponse> FetchPaymentCardsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<PaymentCardsResponse>(VouchaApiEndpoints.PaymentCards(after, limit), cancellationToken);

  public Task<PaymentCardResponse> CreatePaymentCardAsync(
      CreatePaymentCardBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<PaymentCardResponse>(VouchaApiEndpoints.CreatePaymentCard(body), cancellationToken);

  public Task<PaymentCardResponse> UpdatePaymentCardAsync(
      string id,
      UpdatePaymentCardBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<PaymentCardResponse>(VouchaApiEndpoints.UpdatePaymentCard(id, body), cancellationToken);

  public Task DeletePaymentCardAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeletePaymentCard(id), cancellationToken);

  public Task<TopicSearchResponse> SearchPaymentCardTopicsAsync(
      string query,
      int limit = 10,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicSearchResponse>(VouchaApiEndpoints.PaymentCardTopics(query, limit), cancellationToken);
}
