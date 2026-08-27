import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PaymentCardDraftLocaleTests: XCTestCase {
    func testUnrelatedEditParsesCreditLimitWithDraftLocale() throws {
        let card = try JSONDecoder.paymentCards.decode(
            PaymentCardPage.self,
            from: ApiFixtureLoader.data("native.cards.page-1")
        ).results[0]
        let localizedCard = try PaymentCard(
            id: card.id, cardId: card.cardId, openedOn: card.openedOn, closedOn: card.closedOn,
            receivedSignUpBonusOn: card.receivedSignUpBonusOn,
            creditLimit: Money(amount: 123_450, currency: "usd"),
            isAuthorizedUser: card.isAuthorizedUser,
            authorizedUserOfId: card.authorizedUserOfId, note: card.note, card: card.card,
            authorizedUserOfCard: card.authorizedUserOfCard
        )
        var draft = PaymentCardDraft(card: localizedCard, locale: Locale(identifier: "fr_FR"))
        draft.note = "Changed after locale switch"

        let body = try XCTUnwrap(draft.updateBody(comparedWith: localizedCard))

        XCTAssertNil(body.creditLimit)
        XCTAssertNotNil(body.note)
    }

    func testChangingCurrencyToJpyUsesZeroExponentAndProducesPatch() throws {
        let card = try JSONDecoder.paymentCards.decode(
            PaymentCardPage.self,
            from: ApiFixtureLoader.data("native.cards.page-1")
        ).results[0]
        var draft = PaymentCardDraft(card: card, locale: Locale(identifier: "en_US"))
        draft.creditLimitCurrency = "jpy"
        draft.creditLimitText = "123"
        let body = try XCTUnwrap(draft.updateBody(comparedWith: card))
        guard case let .value(limit)? = body.creditLimit else {
            return XCTFail("Expected a credit-limit patch")
        }
        XCTAssertEqual(limit, try Money(amount: 123, currency: "jpy"))
    }
}

private extension JSONDecoder {
    static var paymentCards: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }
}
