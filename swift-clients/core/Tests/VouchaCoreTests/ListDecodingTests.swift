import VouchaModels
import XCTest

final class ListDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()

    func testListsFixtureDecodes() throws {
        let response = try decoder.decode(
            ListsSearchResponse.self,
            from: ApiFixtureLoader.data("native.lists.default")
        )

        XCTAssertEqual(response.results.first?.id, "list-1")
        XCTAssertEqual(response.lists["list-1"]?.name, "Reading Queue")
        XCTAssertEqual(response.lists["list-1"]?.visibility, .private)
    }

    func testListItemsFixtureDecodes() throws {
        let response = try decoder.decode(
            ListItemsResponse.self,
            from: ApiFixtureLoader.data("native.list-items.default")
        )

        XCTAssertEqual(response.results.first?.id, "list-item-1")
        XCTAssertEqual(response.listItems["list-item-1"]?.itemType, .rssFeedItem)
        XCTAssertEqual(response.listItems["list-item-1"]?.mediaType, "article")
    }

    func testListsContainingFixtureDecodes() throws {
        let response = try decoder.decode(
            ListsContainingResponse.self,
            from: ApiFixtureLoader.data("native.lists-containing.default")
        )

        XCTAssertEqual(response.listIds, ["list-1"])
    }

    func testImportCommunityListFixtureDecodes() throws {
        let response = try decoder.decode(
            ImportCommunityListResponse.self,
            from: ApiFixtureLoader.data("native.list-import.default")
        )

        XCTAssertEqual(response.posts, 1)
        XCTAssertEqual(response.items, 1)
    }
}
