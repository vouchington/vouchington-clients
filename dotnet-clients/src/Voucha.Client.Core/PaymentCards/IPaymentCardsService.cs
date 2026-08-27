using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PaymentCards;

public interface IPaymentCardsService
{
  Task<PaymentCardPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<PaymentCard> CreateAsync(string topicId, CancellationToken cancellationToken = default);
  Task<PaymentCard> UpdateAsync(string id, UpdatePaymentCardBody body, CancellationToken cancellationToken = default);
  Task DeleteAsync(string id, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(string query, CancellationToken cancellationToken = default);
}

public sealed record PaymentCardPage(IReadOnlyList<PaymentCard> Results, PageInfo PageInfo);
