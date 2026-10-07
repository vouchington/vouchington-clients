import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class SpendingCategoryEndpointAndModelTests: XCTestCase {
    func testDecodesCursorPageAndDefaultsMissingCapabilityToManageable() throws {
        let page = try makeVouchaDecoder().decode(
            SpendingCategoryPage.self,
            from: Data(
                "{\"results\":[{\"id\":\"entry-1\",\"spending_category_topic_id\":\"topic-1\",\"amount\":{\"amount\":1250,\"currency\":\"usd\"},\"spending_frequency\":\"monthly\",\"note\":null,\"owner_type\":\"household\",\"spending_category\":{\"id\":\"topic-1\",\"name\":\"Dining\",\"slug\":\"dining\"}}],\"page_info\":{\"has_next_page\":true,\"end_cursor\":\"next\"}}"
                    .utf8
            )
        )
        XCTAssertEqual(page.results.first?.amount, try Money(amount: 1_250, currency: "usd"))
        XCTAssertEqual(page.results.first?.spendingFrequency, .monthly)
        XCTAssertTrue(page.results.first?.canManage == true)
        XCTAssertTrue(page.pageInfo.hasNextPage)
        XCTAssertEqual(page.pageInfo.endCursor, "next")
    }

    func testRejectsLegacyAndInvalidMoneyAmounts() {
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            SpendingCategory.self,
            from: Data(
                "{\"id\":\"entry-1\",\"spending_category_topic_id\":\"topic-1\",\"amount\":12.5,\"spending_frequency\":\"monthly\",\"owner_type\":\"individual\",\"spending_category\":{\"id\":\"topic-1\",\"name\":\"Dining\",\"slug\":\"dining\"}}"
                    .utf8
            )
        ))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            SpendingCategory.self,
            from: Data(
                "{\"id\":\"entry-1\",\"spending_category_topic_id\":\"topic-1\",\"amount\":{\"amount\":-1,\"currency\":\"usd\"},\"spending_frequency\":\"monthly\",\"owner_type\":\"individual\",\"spending_category\":{\"id\":\"topic-1\",\"name\":\"Dining\",\"slug\":\"dining\"}}"
                    .utf8
            )
        ))
    }

    func testEncodesNullableFieldsAsExplicitNulls() throws {
        let category = try SpendingCategory(
            id: "entry-1",
            spendingCategoryId: "topic-1",
            amount: Money(amount: 1_250, currency: "usd"),
            spendingFrequency: .monthly,
            note: nil,
            ownerType: .individual,
            spendingCategory: .init(id: "topic-1", name: "Dining", slug: "dining")
        )

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(category)) as? [String: Any])

        XCTAssertNil(encoded["currency_id"])
        XCTAssertEqual((encoded["amount"] as? [String: Any])?["amount"] as? Int, 1_250)
        XCTAssertEqual((encoded["amount"] as? [String: Any])?["currency"] as? String, "usd")
        XCTAssertTrue(encoded["note"] is NSNull)
    }

    func testSpendingCategoryEndpointsUseCursorAndSpendingCategoryTopicFilter() throws {
        XCTAssertEqual(
            Endpoint.spendingCategories(after: "opaque", limit: 2).queryItems,
            [URLQueryItem(name: "limit", value: "2"), URLQueryItem(name: "after", value: "opaque")]
        )
        XCTAssertEqual(
            Endpoint.spendingCategoryTopics(query: "Dining").queryItems,
            [
                URLQueryItem(name: "limit", value: "10"),
                URLQueryItem(name: "q", value: "Dining"),
                URLQueryItem(name: "spending_category", value: "true")
            ]
        )
        try assertEndpoint(
            .createSpendingCategory(body: .init(
                spendingCategoryId: "topic-1",
                amount: Money(amount: 1_250, currency: "usd"),
                spendingFrequency: .annually,
                note: "Annual"
            )),
            method: .POST,
            path: "/api/v1/my/spending-categories",
            body: [
                "spending_category_topic_id": "topic-1",
                "amount": ["amount": 1_250, "currency": "usd"],
                "spending_frequency": "annually",
                "note": "Annual"
            ]
        )
    }
}
