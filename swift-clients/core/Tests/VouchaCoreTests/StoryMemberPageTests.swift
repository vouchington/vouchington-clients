import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class StoryMemberPageTests: XCTestCase {
    func testDecodesBoundedMembershipAndOpaqueContinuation() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let page = try decoder.decode(StoryMemberPage.self, from: Data("""
        {"item_ids":["peer-3","peer-2","peer-1"],
         "page_info":{"has_next_page":true,"end_cursor":"opaque+/="}}
        """.utf8))
        XCTAssertEqual(page.itemIds, ["peer-3", "peer-2", "peer-1"])
        XCTAssertTrue(page.pageInfo.hasNextPage)
        XCTAssertEqual(page.pageInfo.endCursor, "opaque+/=")
    }

    func testStoryEndpointPreservesOpaqueCursorAndExcludedPrimary() {
        let endpoint = Endpoint.story(storyId: "story/one", after: "opaque+/=", excludeItemId: "primary/one")
        XCTAssertEqual(endpoint.path, "/api/v1/stories/story%2Fone")
        XCTAssertEqual(endpoint.method, .GET)
        let query = Dictionary(uniqueKeysWithValues: endpoint.queryItems.map { ($0.name, $0.value) })
        XCTAssertEqual(query["after"], "opaque+/=")
        XCTAssertEqual(query["exclude_item_id"], "primary/one")
        XCTAssertEqual(query["limit"], "25")
    }
}
