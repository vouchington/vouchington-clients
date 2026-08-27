using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PaymentCards;

public sealed class ApiPaymentCardsService : IPaymentCardsService
{
  private readonly VouchaApiClient client;

  public ApiPaymentCardsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public async Task<PaymentCardPage> FetchAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchPaymentCardsAsync(after, limit, cancellationToken).ConfigureAwait(false);
    return new(response.Results.Select(PaymentCard.FromWire).ToArray(), response.PageInfo);
  }

  public async Task<PaymentCard> CreateAsync(string topicId, CancellationToken cancellationToken = default) =>
      PaymentCard.FromWire((await client.CreatePaymentCardAsync(
          new CreatePaymentCardBody(topicId), cancellationToken).ConfigureAwait(false)).Card);

  public async Task<PaymentCard> UpdateAsync(
      string id,
      UpdatePaymentCardBody body,
      CancellationToken cancellationToken = default) =>
      PaymentCard.FromWire((await client.UpdatePaymentCardAsync(id, body, cancellationToken).ConfigureAwait(false)).Card);

  public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeletePaymentCardAsync(id, cancellationToken);

  public async Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(
      string query,
      CancellationToken cancellationToken = default)
  {
    var response = await client.SearchPaymentCardTopicsAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
    return response.Results
        .Where(reference => reference.Id is not null && reference.Name is not null && reference.Slug is not null)
        .Select(reference => new PaymentCardTopic(reference.Id!, reference.Name!, reference.Slug!))
        .ToArray();
  }
}
