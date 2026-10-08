import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class PaymentCardEndpointAndModelTests: XCTestCase {
    func testDecodesCardPagesAndMutationFixtures() throws {
        let decoder = makeVouchaDecoder()
        let first = try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data("native.cards.page-1"))
        let second = try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data("native.cards.page-2"))
        let created = try decoder.decode(
            PaymentCardResponse.self,
            from: ApiFixtureLoader.data("native.cards.create.default")
        )
        let updated = try decoder.decode(
            PaymentCardResponse.self,
            from: ApiFixtureLoader.data("native.cards.update.full")
        )

        XCTAssertEqual(first.results.map(\.card.name), ["Freedom Unlimited", "Everyday Cash"])
        XCTAssertTrue(first.pageInfo.hasNextPage)
        XCTAssertEqual(second.results.single?.creditLimit, try Money(amount: 2_500_000, currency: "usd"))
        XCTAssertEqual(created.card.card.name, "Everyday Cash")
        XCTAssertEqual(updated.card.authorizedUserOfCard?.card.name, "Sapphire Reserve")
        XCTAssertEqual(updated.card.openedOn?.description, "2024-01-20")
    }

    func testLocalDateRejectsInvalidCalendarDates() throws {
        XCTAssertEqual(LocalDate("2024-02-29")?.description, "2024-02-29")
        XCTAssertNil(LocalDate("2023-02-29"))
        XCTAssertNil(LocalDate("2024-2-09"))
        XCTAssertThrowsError(try JSONDecoder().decode(LocalDate.self, from: Data(#""2025-13-01""#.utf8)))
    }

    func testCreditLimitDecodesNestedIntegerMoney() throws {
        let card = try makeVouchaDecoder().decode(
            PaymentCardResponse.self,
            from: ApiFixtureLoader.data("native.cards.update.full")
        ).card
        XCTAssertEqual(card.creditLimit, try Money(amount: 0, currency: "usd"))
    }

    func testCardEndpointsEncodeCursorAndTriStatePatch() throws {
        let page = Endpoint.paymentCards(after: "opaque+/cursor", limit: 2)
        XCTAssertEqual(page.queryItems, [
            URLQueryItem(name: "limit", value: "2"),
            URLQueryItem(name: "after", value: "opaque+/cursor")
        ])
        assertEndpoint(
            .createPaymentCard(body: .init(cardId: "topic-1")),
            method: .POST,
            path: "/api/v1/my/cards",
            body: ["card_topic_id": "topic-1"]
        )
        try assertEndpoint(
            .updatePaymentCard(id: "card/one", body: .init(
                openedOn: .value(XCTUnwrap(LocalDate("2024-01-20"))),
                creditLimit: .value(Money(amount: 1_250, currency: "usd")),
                authorizedUserOfId: .null,
                note: .null
            )),
            method: .PATCH,
            path: "/api/v1/my/cards/card%2Fone",
            body: [
                "opened_on": "2024-01-20",
                "credit_limit": ["amount": 1_250, "currency": "usd"],
                "authorized_user_of_card_id": NSNull(),
                "note": NSNull()
            ]
        )
    }

    func testCardTopicSearchUsesCardFilter() {
        let endpoint = Endpoint.paymentCardTopics(query: "Freedom")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "q", value: "Freedom"),
            URLQueryItem(name: "topic_types", value: "card"),
            URLQueryItem(name: "limit", value: "10")
        ])
    }
}

private extension Array {
    var single: Element? {
        count == 1 ? first : nil
    }
}
