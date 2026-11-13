import Foundation
@testable import VouchaAPI
import VouchaModels

@MainActor
final class InvitationServiceStub: HouseholdInvitationServicing {
    var pages: [String?: HouseholdInvitationPage] = [:]
    var error: Error?
    var requests: [(String?, String?, Int)] = []
    var sentArguments: (String, String, String?)?
    var actions: [String] = []
    var response: HouseholdInvitation?
    var membership: HouseholdMembership?
    var onLoad: (() async throws -> HouseholdInvitationPage)?
    var onUpdate: (() async throws -> Void)?

    func sent(householdId: String, after: String?, limit: Int) async throws -> HouseholdInvitationPage {
        requests.append((householdId, after, limit))
        return try await page(after)
    }

    func received(after: String?, limit: Int) async throws -> HouseholdInvitationPage {
        requests.append((nil, after, limit))
        return try await page(after)
    }

    private func page(_ cursor: String?) async throws -> HouseholdInvitationPage {
        if let onLoad { return try await onLoad() }
        if let error { throw error }
        return pages[cursor] ?? HouseholdInvitationPage(results: [])
    }

    func invite(householdId: String, username: String, relationship: String?) async throws -> HouseholdInvitation {
        sentArguments = (householdId, username, relationship)
        if let error { throw error }
        guard let response else { throw HouseholdTestError.missingStub }
        return response
    }

    func revoke(householdId: String, invitationId: String) async throws {
        actions.append("revoke:\(householdId):\(invitationId)")
        try await update()
    }

    func accept(invitationId: String) async throws -> HouseholdMembership {
        actions.append("accept:\(invitationId)")
        try await update()
        guard let membership else { throw HouseholdTestError.missingStub }
        return membership
    }

    func decline(invitationId: String) async throws {
        actions.append("decline:\(invitationId)")
        try await update()
    }

    private func update() async throws {
        if let onUpdate { try await onUpdate() }
        if let error { throw error }
    }
}

func invitationFixture(_ kind: String = "create", edits: [String: Any] = [:]) throws -> HouseholdInvitation {
    let data = try Data(contentsOf: ApiFixtureLoader.fixtureURL("responses/web.household-invitations.\(kind).json"))
    var object = try JSONSerialization.jsonObject(with: data) as? [String: Any] ?? [:]
    var invitation = object["invitation"] as? [String: Any] ?? [:]
    for (key, value) in edits {
        invitation[key] = value
    }
    object["invitation"] = invitation
    return try APIClient.makeDecoder().decode(
        HouseholdInvitationResponse.self,
        from: JSONSerialization.data(withJSONObject: object)
    ).invitation
}
