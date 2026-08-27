@testable import VouchaAPI
import VouchaModels

extension ApiFixtureEndpointCoverageTests {
    static let paymentCardFixtureEndpoints: [String: Endpoint] = [
        "native.card-topics.search.default": .paymentCardTopics(query: "Freedom", limit: 10),
        "native.cards.empty": .paymentCards(),
        "native.cards.page-1": .paymentCards(limit: 2),
        "native.cards.page-2": .paymentCards(after: pageCursor, limit: 2),
        "native.cards.create.default": .createPaymentCard(body: .init(cardId: topicId)),
        "native.cards.update.full": .updatePaymentCard(id: cardId, body: fullUpdateBody),
        "native.cards.update.clear": .updatePaymentCard(id: cardId, body: clearUpdateBody),
        "native.cards.delete.default": .deletePaymentCard(id: cardId)
    ]

    private static let cardId = "00000000-0000-7000-8000-000000000701"
    private static let topicId = "00000000-0000-7000-8000-000000000713"
    private static let pageCursor = "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcwMiIsInNjb3BlIjoibXktY2FyZHM6MDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwNzAwOmlkLWFzYyJ9"
    private static let fullUpdateBody = UpdatePaymentCardBody(
        openedOn: .value(LocalDate("2024-01-20")!), closedOn: .null,
        receivedSignUpBonusOn: .null,
        creditLimit: .value(try! Money(amount: 0, currency: "usd")),
        isAuthorizedUser: true,
        authorizedUserOfId: .value("00000000-0000-7000-8000-000000000703"),
        note: .value("Authorized-user account")
    )
    private static let clearUpdateBody = UpdatePaymentCardBody(
        openedOn: .null, closedOn: .null, receivedSignUpBonusOn: .null,
        creditLimit: .null, isAuthorizedUser: false, authorizedUserOfId: .null, note: .null
    )
}
