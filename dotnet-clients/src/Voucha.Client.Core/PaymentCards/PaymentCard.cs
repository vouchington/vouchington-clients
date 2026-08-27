using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PaymentCards;

public sealed record PaymentCard(
    string Id,
    string CardId,
    DateOnly? OpenedOn,
    DateOnly? ClosedOn,
    DateOnly? ReceivedSignUpBonusOn,
    Money? CreditLimit,
    bool IsAuthorizedUser,
    string? AuthorizedUserOfId,
    string? Note,
    PaymentCardTopic Card,
    PaymentCardParentSummary? AuthorizedUserOfCard)
{
  internal static PaymentCard FromWire(PaymentCardWire wire)
  {
    ArgumentNullException.ThrowIfNull(wire);
    return new(
        wire.Id, wire.CardId, wire.OpenedOn, wire.ClosedOn, wire.ReceivedSignUpBonusOn,
        wire.CreditLimit, wire.IsAuthorizedUser, wire.AuthorizedUserOfId, wire.Note, wire.Card,
        wire.AuthorizedUserOfCard);
  }

  internal PaymentCard WithoutDeletedParent(string deletedId) =>
      AuthorizedUserOfId == deletedId || AuthorizedUserOfCard?.Id == deletedId
          ? this with { AuthorizedUserOfId = null, AuthorizedUserOfCard = null }
          : this;
}
