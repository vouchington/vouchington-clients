import VouchaModels

@MainActor
extension PaymentCardsViewModel {
    func parentChoices(for card: PaymentCard, selectedParentId: String?) -> [PaymentCard] {
        var choices = cards.filter { $0.id != card.id && ($0.closedOn == nil || $0.id == selectedParentId) }
        if let summary = card.authorizedUserOfCard,
           summary.id == selectedParentId,
           !choices.contains(where: { $0.id == summary.id }) {
            choices.append(summary.asPaymentCard)
        }
        return choices.sorted { $0.id < $1.id }
    }
}

private extension PaymentCardParentSummary {
    var asPaymentCard: PaymentCard {
        PaymentCard(
            id: id, cardId: card.id, openedOn: openedOn, closedOn: closedOn,
            receivedSignUpBonusOn: nil, creditLimit: nil, isAuthorizedUser: false,
            authorizedUserOfId: nil, note: nil, card: card, authorizedUserOfCard: nil
        )
    }
}
