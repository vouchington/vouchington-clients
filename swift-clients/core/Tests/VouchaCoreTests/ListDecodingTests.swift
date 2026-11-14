import Foundation
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
        XCTAssertEqual(response.listItems["list-item-1"]?.entityLabel, "Saved article")
        let unlabeled = try XCTUnwrap(response.listItems["list-item-2"])
        XCTAssertEqual(unlabeled.itemType, .post)
        XCTAssertNil(unlabeled.entityLabel)
    }

    func testSavedListFixtureRoundTripsLabelAndNullFacts() throws {
        try assertFixtureCoversDTO(
            "native.list-items.default", as: ListItemsResponse.self,
            ignoring: [
                "list_items.list-item-1.__entity_type",
                "list_items.list-item-2.__entity_type",
                "results.__entity_type"
            ]
        )
    }

    func testListItemReadAndAddRequireNullableLabel() throws {
        let object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.list-items.default")
        ) as? [String: Any])
        let items = try XCTUnwrap(object["list_items"] as? [String: [String: Any]])
        for id in ["list-item-1", "list-item-2"] {
            var item = try XCTUnwrap(items[id])
            let added = try decoder.decode(
                ListItemResponse.self,
                from: JSONSerialization.data(withJSONObject: ["list_item": item])
            )
            XCTAssertEqual(added.listItem.entityLabel, id == "list-item-1" ? "Saved article" : nil)
            item.removeValue(forKey: "entity_label")
            XCTAssertThrowsError(try decoder.decode(
                ListItemResponse.self,
                from: JSONSerialization.data(withJSONObject: ["list_item": item])
            ))
            XCTAssertThrowsError(try decoder.decode(
                ListItem.self, from: JSONSerialization.data(withJSONObject: item)
            ))
        }
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
