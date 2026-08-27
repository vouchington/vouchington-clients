import VouchaAPI
import VouchaModels

@MainActor
protocol HouseholdServicing {
    func households(access: HouseholdAccess, after: String?, limit: Int) async throws -> HouseholdPage
    func createHousehold() async throws -> Household
    func memberships(householdId: String, after: String?, limit: Int) async throws -> HouseholdMembershipPage
    func removeMembership(householdId: String, membershipId: String) async throws
}

@MainActor
final class HouseholdService: HouseholdServicing {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func households(access: HouseholdAccess, after: String?, limit: Int) async throws -> HouseholdPage {
        try await client.send(.households(access: access, after: after, limit: limit))
    }

    func createHousehold() async throws -> Household {
        let response: HouseholdCreateResponse = try await client.send(.createHousehold)
        return response.household
    }

    func memberships(householdId: String, after: String?, limit: Int) async throws -> HouseholdMembershipPage {
        try await client.send(.householdMemberships(householdId: householdId, after: after, limit: limit))
    }

    func removeMembership(householdId: String, membershipId: String) async throws {
        let _: HouseholdEmptyResponse = try await client.send(
            .deleteHouseholdMembership(householdId: householdId, membershipId: membershipId)
        )
    }
}

private struct HouseholdEmptyResponse: Decodable {}
