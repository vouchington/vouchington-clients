import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdServiceStub: HouseholdServicing {
    var ownedHouseholdPage = HouseholdPage(results: [])
    var memberHouseholdPages: [String?: HouseholdPage] = [:]
    var createdHousehold: Household?
    var membershipPages: [String: HouseholdMembershipPage] = [:]
    var ownedHouseholdError: Error?
    var memberHouseholdErrors: [String?: Error] = [:]
    var membershipErrors: [String: Error] = [:]
    var createCalls = 0
    var membershipCalls: [String: Int] = [:]
    var removalCalls: [String: Int] = [:]
    var membershipHandler: ((String) async throws -> HouseholdMembershipPage)?
    var paginatedMembershipHandler: ((String, String?) async throws -> HouseholdMembershipPage)?
    var householdHandler: ((HouseholdAccess, String?) async throws -> HouseholdPage)?
    var removalHandler: ((String, String) async throws -> Void)?

    func households(access: HouseholdAccess, after: String?, limit _: Int) async throws -> HouseholdPage {
        if let householdHandler {
            return try await householdHandler(access, after)
        }
        if access == .owned {
            if let ownedHouseholdError {
                throw ownedHouseholdError
            }
            return ownedHouseholdPage
        }
        if let error = memberHouseholdErrors[after] {
            throw error
        }
        return memberHouseholdPages[after] ?? HouseholdPage(results: [])
    }

    func createHousehold() async throws -> Household {
        createCalls += 1
        guard let createdHousehold else { throw HouseholdTestError.missingStub }
        return createdHousehold
    }

    func memberships(householdId: String, after: String?, limit _: Int) async throws -> HouseholdMembershipPage {
        membershipCalls[householdId, default: 0] += 1
        if let paginatedMembershipHandler {
            return try await paginatedMembershipHandler(householdId, after)
        }
        if let membershipHandler {
            return try await membershipHandler(householdId)
        }
        if let error = membershipErrors[householdId] {
            throw error
        }
        return membershipPages[householdId] ?? HouseholdMembershipPage(results: [])
    }

    func removeMembership(householdId: String, membershipId: String) async throws {
        removalCalls[membershipId, default: 0] += 1
        try await removalHandler?(householdId, membershipId)
    }
}

enum HouseholdTestError: Error {
    case failed
    case missingStub
}

let householdTestDate = Date(timeIntervalSince1970: 1_700_000_000)

func makeHousehold(id: String, ownerId: String) -> Household {
    Household(id: id, ownerId: ownerId, updatedAt: householdTestDate)
}

func makeHouseholdMembership(
    id: String,
    householdId: String,
    username: String? = "alice",
    relationship: String? = "spouse"
) -> HouseholdMembership {
    HouseholdMembership(
        id: id,
        householdId: householdId,
        individual: HouseholdIndividual(id: "individual-\(id)", username: username, updatedAt: householdTestDate),
        relationship: relationship,
        updatedAt: householdTestDate
    )
}

@MainActor
func makeHouseholdViewModel(
    service: HouseholdServiceStub? = nil
) -> HouseholdViewModel {
    HouseholdViewModel(service: service ?? HouseholdServiceStub())
}

@MainActor
func setLoadedMemberHouseholds(_ households: [Household], on viewModel: HouseholdViewModel) {
    var pagination = CursorPaginationState<Household>()
    guard let request = pagination.beginInitialPageIfNeeded() else {
        XCTFail("Expected an initial member-household request")
        return
    }
    pagination.complete(request, items: households, endCursor: nil, hasNextPage: false)
    viewModel.memberHouseholdPagination = pagination
}
