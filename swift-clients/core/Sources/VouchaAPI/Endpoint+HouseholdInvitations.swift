import Foundation

private struct HouseholdInvitationBody: Encodable {
    let user: String
    let relationship: String?
}

private struct HouseholdInvitationActionBody: Encodable {}

public extension Endpoint {
    static func householdInvitations(householdId: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: householdInvitationPath(householdId), after: after, limit: limit)
    }

    static func receivedHouseholdInvitations(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/my/household-invitations", after: after, limit: limit)
    }

    static func createHouseholdInvitation(
        householdId: String,
        username: String,
        relationship: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: householdInvitationPath(householdId),
            body: HouseholdInvitationBody(user: username, relationship: relationship)
        )
    }

    static func revokeHouseholdInvitation(householdId: String, invitationId: String) -> Endpoint {
        Endpoint(.DELETE, path: "\(householdInvitationPath(householdId))/\(pathSegment(invitationId))")
    }

    static func acceptHouseholdInvitation(invitationId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/household-invitations/\(pathSegment(invitationId))/acceptance",
            body: HouseholdInvitationActionBody()
        )
    }

    static func declineHouseholdInvitation(invitationId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/household-invitations/\(pathSegment(invitationId))/decline",
            body: HouseholdInvitationActionBody()
        )
    }

    private static func householdInvitationPath(_ householdId: String) -> String {
        "/api/v1/households/\(pathSegment(householdId))/invitations"
    }
}
