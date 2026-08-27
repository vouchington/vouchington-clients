import Foundation

private struct ListMutationBody: Encodable {
    let name: String?
    let description: NullableStringPatchField?
    let visibility: String?
}

private struct ListRssFeedItemBody: Encodable {
    let rssFeedItemId: String
}

private struct ListPostBody: Encodable {
    let postId: String
}

private struct ImportCommunityListBody: Encodable {
    let communitySlug: String
}

public extension Endpoint {
    static func lists(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/lists", queryItems: items)
    }

    static func createList(name: String, description: String? = nil, visibility: String = "private") -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/lists",
            body: ListMutationBody(
                name: name,
                description: description.map(NullableStringPatchField.init),
                visibility: visibility
            )
        )
    }

    static func list(listId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/lists/\(pathSegment(listId))")
    }

    static func updateList(
        listId: String,
        name: String? = nil,
        description: NullableStringPatchField? = nil,
        visibility: String? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/lists/\(pathSegment(listId))",
            body: ListMutationBody(name: name, description: description, visibility: visibility)
        )
    }

    static func deleteList(listId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/lists/\(pathSegment(listId))")
    }

    static func listItems(
        listId: String,
        mediaType: String? = nil,
        read: Bool? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let mediaType {
            items.append(.init(name: "media_type", value: mediaType))
        }
        if let read {
            items.append(.init(name: "read", value: read ? "true" : "false"))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/lists/\(pathSegment(listId))/items", queryItems: items)
    }

    static func addListRssFeedItem(listId: String, rssFeedItemId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/lists/\(pathSegment(listId))/items/rss-feed-items",
            body: ListRssFeedItemBody(rssFeedItemId: rssFeedItemId)
        )
    }

    static func removeListRssFeedItem(listId: String, rssFeedItemId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/lists/\(pathSegment(listId))/items/rss-feed-items/\(pathSegment(rssFeedItemId))"
        )
    }

    static func addListPost(listId: String, postId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/lists/\(pathSegment(listId))/items/posts",
            body: ListPostBody(postId: postId)
        )
    }

    static func removeListPost(listId: String, postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/lists/\(pathSegment(listId))/items/posts/\(pathSegment(postId))")
    }

    static func listsContaining(itemType: String, entityId: String) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/lists/contains",
            queryItems: [
                URLQueryItem(name: "item_type", value: itemType),
                URLQueryItem(name: "entity_id", value: entityId)
            ]
        )
    }

    static func importCommunityList(listId: String, communitySlug: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/lists/\(pathSegment(listId))/import",
            body: ImportCommunityListBody(communitySlug: communitySlug)
        )
    }

    static func markRssFeedItemRead(rssFeedItemId: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/read")
    }

    static func markRssFeedItemUnread(rssFeedItemId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/read")
    }

    static func markPostRead(postId: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/posts/\(pathSegment(postId))/read")
    }

    static func markPostUnread(postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/posts/\(pathSegment(postId))/read")
    }
}
