import Foundation

private struct CreateHouseholdBody: Encodable {}

public enum HouseholdAccess: String, Sendable {
    case all
    case owned
    case member
}

public extension Endpoint {
    static func households(
        access: HouseholdAccess = .all,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if access != .all {
            queryItems.append(.init(name: "access", value: access.rawValue))
        }
        if let limit {
            queryItems.append(.init(name: "limit", value: "\(limit)"))
        }
        if let after {
            queryItems.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/households", queryItems: queryItems)
    }

    static var createHousehold: Endpoint {
        Endpoint(.POST, path: "/api/v1/households", body: CreateHouseholdBody())
    }

    static func householdMemberships(
        householdId: String,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(.init(name: "limit", value: "\(limit)"))
        }
        if let after {
            queryItems.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/households/\(pathSegment(householdId))/memberships",
            queryItems: queryItems
        )
    }

    static func deleteHouseholdMembership(householdId: String, membershipId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/households/\(pathSegment(householdId))/memberships/\(pathSegment(membershipId))"
        )
    }
}
