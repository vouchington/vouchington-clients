@testable import VouchaModels
import XCTest

final class RssFeedItemCategoryCompatibilityTests: XCTestCase {
    func testDecodesCategoryWhenHashtagSidecarIsOmitted() throws {
        let json = Data(
            #"""
            {
              "id": "cat-1",
              "category_text": "technology",
              "topic": null,
              "votes_score_net": null
            }
            """#
            .utf8
        )

        let category = try makeVouchaDecoder().decode(RssFeedItemCategoryModel.self, from: json)

        XCTAssertEqual(category.id, "cat-1")
        XCTAssertEqual(category.categoryText, "technology")
        XCTAssertNil(category.hashtag)
    }
}
