import Foundation
import VouchaModels

public struct CreatePointValuationBody: Encodable, Sendable {
    public let rewardsProgramId: String
    public let valuePerPoint: ScaledMoney
    public let note: String?

    public init(rewardsProgramId: String, valuePerPoint: ScaledMoney, note: String? = nil) {
        self.rewardsProgramId = rewardsProgramId
        self.valuePerPoint = valuePerPoint
        self.note = note
    }
}

public struct UpdatePointValuationBody: Encodable, Sendable {
    public let valuePerPoint: ScaledMoney?
    public let note: NullableValue<String>?

    public init(valuePerPoint: ScaledMoney? = nil, note: NullableValue<String>? = nil) {
        self.valuePerPoint = valuePerPoint
        self.note = note
    }
}

public extension Endpoint {
    static func pointValuations(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: String(limit))]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/rewards-program-point-valuations", queryItems: queryItems)
    }

    static func createPointValuation(body: CreatePointValuationBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/rewards-program-point-valuations", body: body)
    }

    static func updatePointValuation(id: String, body: UpdatePointValuationBody) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/rewards-program-point-valuations/\(pathSegment(id))",
            body: body
        )
    }

    static func deletePointValuation(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/rewards-program-point-valuations/\(pathSegment(id))")
    }

    static func rewardsProgramTopics(query: String, limit: Int = 10) -> Endpoint {
        topics(query: query, topicTypes: ["rewards_program"], limit: limit)
    }
}
