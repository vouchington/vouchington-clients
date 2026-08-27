@testable import VouchaAPI
import VouchaModels
import XCTest

final class AiCostTotalsTests: XCTestCase {
    func testDecodesNativeAiCostFixturesWithInt64CountsAndExactAggregateMoney() throws {
        let page = try makeVouchaDecoder().decode(
            AiCostTotalsPage.self,
            from: ApiFixtureLoader.data("native.admin-ai-costs.default")
        )

        XCTAssertTrue(page.pageInfo.hasNextPage)
        XCTAssertNotNil(page.pageInfo.endCursor)
        XCTAssertGreaterThan(page.results[0].totalInputTokens, Int64(Int32.max))
        XCTAssertEqual(page.results[0].totalCost.amount, "1234567890123456789012345")
        XCTAssertEqual(page.results[0].totalCost.scale, 6)
    }

    func testAiCostEndpointUsesFixedPageSizeAndForwardsOpaqueCursor() {
        let endpoint = Endpoint.adminAiCosts(after: "opaque+cursor==")

        XCTAssertEqual(endpoint.path, "/api/v1/admin/ai-costs")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "after", value: "opaque+cursor==")
        ])
    }
}
