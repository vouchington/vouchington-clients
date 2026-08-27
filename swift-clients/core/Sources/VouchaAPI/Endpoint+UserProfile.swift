import Foundation
import VouchaModels

public extension Endpoint {
    static func voteUserTrust(userId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/users/\(pathSegment(userId))/vouch-vote", body: ["choice": choice.rawValue])
    }

    static func clearUserTrustVote(userId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/users/\(pathSegment(userId))/vouch-vote")
    }

    static func userTrustContext(userId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/users/\(pathSegment(userId))/vouch-context")
    }

    static func userPosts(
        userId: String,
        postTypes: String? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items = [
            URLQueryItem(name: "creator", value: userId),
            URLQueryItem(name: "limit", value: "\(limit)"),
            URLQueryItem(name: "sort", value: "new")
        ]
        if let postTypes {
            items.append(.init(name: "post_types", value: postTypes))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/posts", queryItems: items)
    }

    static func userTopicsFollowing(userId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        profileCollection(userId: userId, path: "topics/following", after: after, limit: limit)
    }

    static func userCommunitiesMember(userId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        profileCollection(userId: userId, path: "communities/member", after: after, limit: limit)
    }

    private static func profileCollection(
        userId: String,
        path: String,
        after: String?,
        limit: Int
    ) -> Endpoint {
        var items = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/users/\(pathSegment(userId))/\(path)", queryItems: items)
    }
}
