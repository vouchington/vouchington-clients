import Foundation
import VouchaModels

public struct CreateRewardsProgramStatusBody: Encodable, Sendable {
    public let rewardsProgramStatusId: String

    public init(rewardsProgramStatusId: String) {
        self.rewardsProgramStatusId = rewardsProgramStatusId
    }
}

public struct UpdateRewardsProgramStatusBody: Encodable, Sendable {
    public let since: NullableValue<LocalDate>?
    public let until: NullableValue<LocalDate>?

    public init(since: NullableValue<LocalDate>? = nil, until: NullableValue<LocalDate>? = nil) {
        self.since = since
        self.until = until
    }
}

public extension Endpoint {
    static func rewardsProgramStatuses(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: String(limit))]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/rewards-program-statuses", queryItems: queryItems)
    }

    static func createRewardsProgramStatus(body: CreateRewardsProgramStatusBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/rewards-program-statuses", body: body)
    }

    static func updateRewardsProgramStatus(id: String, body: UpdateRewardsProgramStatusBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/rewards-program-statuses/\(pathSegment(id))", body: body)
    }

    static func deleteRewardsProgramStatus(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/rewards-program-statuses/\(pathSegment(id))")
    }

    static func rewardsProgramStatusTopics(query: String, limit: Int = 10) -> Endpoint {
        topics(query: query, topicTypes: ["rewards_program_status"], limit: limit)
    }
}
