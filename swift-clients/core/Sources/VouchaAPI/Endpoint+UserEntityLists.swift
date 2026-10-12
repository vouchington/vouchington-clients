import Foundation

public extension Endpoint {
    static func userPosts(
        userId: String,
        listType: String,
        mediaType: String? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        userEntityList(
            userId: userId,
            entitySegment: "posts",
            listType: listType,
            after: after,
            limit: limit,
            queryItems: mediaType.map { [URLQueryItem(name: "media_type", value: $0)] } ?? []
        )
    }

    static func userTopics(userId: String, listType: String, limit: Int = 25) -> Endpoint {
        userEntityList(userId: userId, entitySegment: "topics", listType: listType, limit: limit)
    }

    static func userUsers(userId: String, listType: String, limit: Int = 25) -> Endpoint {
        userEntityList(userId: userId, entitySegment: "users", listType: listType, limit: limit)
    }

    static func userCommunities(userId: String, listType: String, limit: Int = 25) -> Endpoint {
        userEntityList(userId: userId, entitySegment: "communities", listType: listType, limit: limit)
    }

    static func userUrls(userId: String, listType: String, limit: Int = 25) -> Endpoint {
        userEntityList(userId: userId, entitySegment: "urls", listType: listType, limit: limit)
    }

    static func userHostnames(userId: String, listType: String, limit: Int = 25) -> Endpoint {
        userEntityList(userId: userId, entitySegment: "domains", listType: listType, limit: limit)
    }

    static func userRssFeedItems(
        userId: String,
        listType: String,
        mediaType: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        userEntityList(
            userId: userId,
            entitySegment: "rss-feed-items",
            listType: listType,
            limit: limit,
            queryItems: mediaType.map { [URLQueryItem(name: "media_type", value: $0)] } ?? []
        )
    }

    private static func userEntityList(
        userId: String,
        entitySegment: String,
        listType: String,
        after: String? = nil,
        limit: Int,
        queryItems: [URLQueryItem] = []
    ) -> Endpoint {
        pagedGet(
            path: "/api/v1/users/\(pathSegment(userId))/\(entitySegment)/\(pathSegment(listType))",
            after: after,
            limit: limit,
            queryItems: queryItems
        )
    }
}
