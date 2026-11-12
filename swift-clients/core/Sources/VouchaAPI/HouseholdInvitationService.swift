import VouchaModels

@MainActor
public protocol HouseholdInvitationServicing {
    func sent(householdId: String, after: String?, limit: Int) async throws -> HouseholdInvitationPage
    func received(after: String?, limit: Int) async throws -> HouseholdInvitationPage
    func invite(householdId: String, username: String, relationship: String?) async throws -> HouseholdInvitation
    func revoke(householdId: String, invitationId: String) async throws
    func accept(invitationId: String) async throws -> HouseholdMembership
    func decline(invitationId: String) async throws
}

@MainActor
public final class HouseholdInvitationService: HouseholdInvitationServicing {
    private let client: APIClient

    public init(client: APIClient) {
        self.client = client
    }

    public func sent(householdId: String, after: String?, limit: Int) async throws -> HouseholdInvitationPage {
        try await client.send(.householdInvitations(householdId: householdId, after: after, limit: limit))
    }

    public func received(after: String?, limit: Int) async throws -> HouseholdInvitationPage {
        try await client.send(.receivedHouseholdInvitations(after: after, limit: limit))
    }

    public func invite(
        householdId: String,
        username: String,
        relationship: String?
    ) async throws -> HouseholdInvitation {
        let response: HouseholdInvitationResponse = try await client.send(
            .createHouseholdInvitation(householdId: householdId, username: username, relationship: relationship)
        )
        return response.invitation
    }

    public func revoke(householdId: String, invitationId: String) async throws {
        let _: EmptyInvitationResponse = try await client.send(
            .revokeHouseholdInvitation(householdId: householdId, invitationId: invitationId)
        )
    }

    public func accept(invitationId: String) async throws -> HouseholdMembership {
        let response: HouseholdInvitationAcceptanceResponse = try await client.send(
            .acceptHouseholdInvitation(invitationId: invitationId)
        )
        return response.membership
    }

    public func decline(invitationId: String) async throws {
        let _: EmptyInvitationResponse = try await client.send(.declineHouseholdInvitation(invitationId: invitationId))
    }
}

private struct EmptyInvitationResponse: Decodable {}
