import Foundation
import VouchaModels

private struct CommunityApplicationBody: Encodable {
    let answers: [String: DecodedJSONValue]
    let message: String?
}

private struct CommunityApplicationQuestionsBody: Encodable {
    let questions: [CommunityApplicationQuestionInput]
}

private struct CommunityPinnedPostsBody: Encodable {
    let postIds: [String]
}

public extension Endpoint {
    static func communityApplicationQuestions(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/application-questions")
    }

    static func setCommunityApplicationQuestions(
        idOrSlug: String,
        questions: [CommunityApplicationQuestionInput]
    ) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/application-questions",
            body: CommunityApplicationQuestionsBody(questions: questions)
        )
    }

    static func communityApplications(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 25,
        status: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let status {
            items.append(.init(name: "status", value: status))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/applications", queryItems: items)
    }

    static func submitCommunityApplication(
        idOrSlug: String,
        answers: [String: DecodedJSONValue],
        message: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/applications",
            body: CommunityApplicationBody(answers: answers, message: message)
        )
    }

    static func communityPinnedPosts(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/pinned-posts")
    }

    static func updateCommunityPinnedPosts(idOrSlug: String, postIds: [String]) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/pinned-posts",
            body: CommunityPinnedPostsBody(postIds: postIds)
        )
    }

    static func communityPendingPosts(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/pending", queryItems: items)
    }

    static func communityPendingReports(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 50,
        sort: String = "created_at_desc"
    ) -> Endpoint {
        var items: [URLQueryItem] = [
            .init(name: "limit", value: "\(limit)"),
            .init(name: "sort", value: sort)
        ]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/pending",
            queryItems: items
        )
    }
}
