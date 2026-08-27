@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class UserTagEndpointTests: XCTestCase {
    func testUserTagsEndpointUsesAuthenticatedCatalogRoute() {
        XCTAssertEqual(Endpoint.userTags().path, "/api/v1/topics/user-tags")
    }

    func testDecodesUserTagCatalog() throws {
        let data = Data(#"{"user_tags":[{"id":"bot-id","slug":"bot","label":"Bot"}]}"#.utf8)
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(UserTagsResponse.self, from: data)
        XCTAssertEqual(response.userTags.map(\.label), ["Bot"])
    }
}
