import Foundation

public extension Endpoint {
    static func memberWarningNotices(after: String? = nil, limit: Int = 25) -> Endpoint {
        memberAppealNotices(path: "/api/v1/my/warnings", after: after, limit: limit)
    }

    static func memberCommunityBanNotices(after: String? = nil, limit: Int = 25) -> Endpoint {
        memberAppealNotices(path: "/api/v1/my/bans", after: after, limit: limit)
    }

    static func memberRemovedPostNotices(after: String? = nil, limit: Int = 25) -> Endpoint {
        memberAppealNotices(
            path: "/api/v1/my/removed-posts",
            after: after,
            limit: limit,
            includePlatform: true
        )
    }

    private static func memberAppealNotices(
        path: String,
        after: String?,
        limit: Int,
        includePlatform: Bool = false
    ) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: String(limit))]
        if includePlatform {
            queryItems.append(URLQueryItem(name: "include_platform", value: "true"))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: path, queryItems: queryItems)
    }
}
