import Foundation
@testable import VouchaFeatures
import VouchaModels

extension ListsViewModelTests {
    static let listsData = Data("""
    {
      "results": [{ "id": "list-1" }],
      "page_info": { "has_next_page": false },
      "lists": {
        "list-1": {
          "id": "list-1",
          "owner_user_id": "user-1",
          "name": "Reading Queue",
          "description": "Saved articles",
          "visibility": "private",
          "created_at": "2026-06-28T10:00:00Z",
          "updated_at": "2026-06-28T10:00:00Z",
          "removed_at": null
        }
      }
    }
    """.utf8)

    static let itemsData = Data("""
    {
      "results": [{ "id": "list-item-1" }, { "id": "list-item-2" }],
      "page_info": { "has_next_page": false },
      "list_items": {
        "list-item-1": {
          "id": "list-item-1",
          "list_id": "list-1",
          "item_type": "rss_feed_item",
          "entity_id": "item-1",
          "order_index": 0,
          "created_at": "2026-06-28T10:05:00Z",
          "media_type": "article"
        },
        "list-item-2": {
          "id": "list-item-2",
          "list_id": "list-1",
          "item_type": "post",
          "entity_id": "post-1",
          "order_index": 1,
          "created_at": "2026-06-28T10:06:00Z",
          "media_type": "discussion"
        }
      }
    }
    """.utf8)

    static let list2ItemsData = Data("""
    {
      "results": [{ "id": "list-item-3" }],
      "page_info": { "has_next_page": false },
      "list_items": {
        "list-item-3": {
          "id": "list-item-3",
          "list_id": "list-2",
          "item_type": "post",
          "entity_id": "post-2",
          "order_index": 0,
          "created_at": "2026-06-28T10:07:00Z",
          "media_type": "discussion"
        }
      }
    }
    """.utf8)

    static let createdListData = listResponse(id: "list-created", name: "Reading", description: "Saved")
    static let updatedListData = listResponse(id: "list-created", name: "Renamed", description: "Updated")
    static let clearedListData = listResponse(id: "list-created", name: "Reading", description: nil)

    static func listResponse(id: String, name: String, description: String?) -> Data {
        Data("""
        {
          "list": {
            "id": "\(id)",
            "owner_user_id": "user-1",
            "name": "\(name)",
            "description": \(description.map { "\"\($0)\"" } ?? "null"),
            "visibility": "private",
            "created_at": "2026-06-28T10:00:00Z",
            "updated_at": "2026-06-28T10:00:00Z",
            "removed_at": null
          }
        }
        """.utf8)
    }

    static func userList(id: String, name: String, description: String?) throws -> UserList {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(ListResponse.self, from: listResponse(id: id, name: name, description: description))
            .list
    }
}
