import Foundation

public extension Endpoint {
    static func growthMetrics(range: GrowthRange = .thirtyDays) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/growth-metrics",
            queryItems: [.init(name: "range", value: range.rawValue)]
        )
    }
}
