@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdViewModelRaceTests: XCTestCase {
    func testStaleOwnedAndMemberResponsesCannotReplaceReload() async {
        let service = HouseholdServiceStub()
        var ownedContinuations: [CheckedContinuation<HouseholdPage, Error>] = []
        var memberContinuations: [CheckedContinuation<HouseholdPage, Error>] = []
        service.householdHandler = { access, _ in
            try await withCheckedThrowingContinuation { continuation in
                if access == .owned {
                    ownedContinuations.append(continuation)
                } else {
                    memberContinuations.append(continuation)
                }
            }
        }
        let viewModel = makeHouseholdViewModel(service: service)

        let stale = Task { await viewModel.load() }
        await waitUntil { ownedContinuations.count == 1 && memberContinuations.count == 1 }
        let fresh = Task { await viewModel.load() }
        await waitUntil { ownedContinuations.count == 2 && memberContinuations.count == 2 }
        ownedContinuations[1].resume(returning: HouseholdPage(results: [
            makeHousehold(id: "owned-fresh", ownerId: "owner")
        ]))
        memberContinuations[1].resume(returning: HouseholdPage(results: [
            makeHousehold(id: "member-fresh", ownerId: "other")
        ]))
        await fresh.value
        ownedContinuations[0].resume(returning: HouseholdPage(results: [
            makeHousehold(id: "owned-stale", ownerId: "owner")
        ]))
        memberContinuations[0].resume(returning: HouseholdPage(results: [
            makeHousehold(id: "member-stale", ownerId: "other")
        ]))
        await stale.value

        XCTAssertEqual(viewModel.ownedHousehold?.id, "owned-fresh")
        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), ["member-fresh"])
    }

    func testSuccessfulRemovalTombstoneFiltersInFlightContinuation() async throws {
        let context = makeRaceContext()
        context.viewModel.sections[0].resetForReload()
        let request = try XCTUnwrap(context.viewModel.sections[0].beginInitialPageIfNeeded())
        context.viewModel.sections[0].complete(
            request,
            page: HouseholdMembershipPage(
                results: [context.member],
                pageInfo: .init(hasNextPage: true, endCursor: "members-next")
            ),
            excluding: []
        )
        var continuation: CheckedContinuation<HouseholdMembershipPage, Error>?
        context.service.paginatedMembershipHandler = { _, _ in
            try await withCheckedThrowingContinuation { continuation = $0 }
        }

        let nextPage = Task {
            await context.viewModel.loadNextMemberships(householdId: context.household.id)
        }
        await waitUntil { continuation != nil }
        await context.viewModel.removeMembership(context.member, householdId: context.household.id)
        continuation?.resume(returning: HouseholdMembershipPage(results: [context.member]))
        await nextPage.value

        XCTAssertTrue(context.viewModel.sections[0].members.isEmpty)
    }

    func testMembershipLoadStartedBeforeSuccessfulRemovalCannotRestoreMember() async {
        let context = makeRaceContext()
        var load: CheckedContinuation<HouseholdMembershipPage, Error>?
        context.service.membershipHandler = { _ in
            try await withCheckedThrowingContinuation { load = $0 }
        }
        context.viewModel.sections[0].invalidateForMembershipMutation()
        let loadTask = Task { await context.viewModel.loadMemberships(householdId: context.household.id) }
        await waitUntil { load != nil }

        await context.viewModel.removeMembership(context.member, householdId: context.household.id)
        load?.resume(returning: HouseholdMembershipPage(results: [context.member]))
        await loadTask.value

        XCTAssertTrue(context.viewModel.sections[0].members.isEmpty)
        XCTAssertFalse(context.viewModel.sections[0].isLoading)
    }

    func testMembershipLoadStartedBeforeFailedRemovalCannotReplaceRollback() async {
        let context = makeRaceContext()
        var load: CheckedContinuation<HouseholdMembershipPage, Error>?
        context.service.membershipHandler = { _ in
            try await withCheckedThrowingContinuation { load = $0 }
        }
        context.service.removalHandler = { _, _ in throw HouseholdTestError.failed }
        context.viewModel.sections[0].invalidateForMembershipMutation()
        let loadTask = Task { await context.viewModel.loadMemberships(householdId: context.household.id) }
        await waitUntil { load != nil }

        await context.viewModel.removeMembership(context.member, householdId: context.household.id)
        let stale = makeHouseholdMembership(id: "stale", householdId: context.household.id)
        load?.resume(returning: HouseholdMembershipPage(results: [stale]))
        await loadTask.value

        XCTAssertEqual(context.viewModel.sections[0].members.map(\.id), [context.member.id])
        XCTAssertFalse(context.viewModel.sections[0].isLoading)
    }

    func testConcurrentFailuresRestoreOriginalOrderInEitherCompletionOrder() async {
        for reverseFailures in [false, true] {
            let service = HouseholdServiceStub()
            let household = makeHousehold(id: "owned", ownerId: "owner")
            let members = ["a", "b", "c"].map {
                makeHouseholdMembership(id: $0, householdId: household.id)
            }
            var removals: [String: CheckedContinuation<Void, Error>] = [:]
            service.removalHandler = { _, id in
                try await withCheckedThrowingContinuation { removals[id] = $0 }
            }
            let viewModel = makeHouseholdViewModel(service: service)
            viewModel.sections = [section(household: household, members: members)]

            let first = Task { await viewModel.removeMembership(members[0], householdId: household.id) }
            let second = Task { await viewModel.removeMembership(members[1], householdId: household.id) }
            await waitUntil { removals.count == 2 }
            for id in reverseFailures ? ["b", "a"] : ["a", "b"] {
                removals[id]?.resume(throwing: HouseholdTestError.failed)
                await Task.yield()
            }
            await first.value
            await second.value

            XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["a", "b", "c"])
        }
    }

    func testMembershipLoadStartedDuringSuccessfulRemovalCannotRestoreMember() async {
        let context = makeRaceContext()
        var removal: CheckedContinuation<Void, Error>?
        var load: CheckedContinuation<HouseholdMembershipPage, Error>?
        context.service.removalHandler = { _, _ in
            try await withCheckedThrowingContinuation { removal = $0 }
        }
        context.service.membershipHandler = { _ in
            try await withCheckedThrowingContinuation { load = $0 }
        }
        let removeTask = Task {
            await context.viewModel.removeMembership(context.member, householdId: context.household.id)
        }
        await waitUntil { removal != nil }
        let loadTask = Task { await context.viewModel.loadMemberships(householdId: context.household.id) }
        await waitUntil { load != nil }
        removal?.resume()
        await removeTask.value
        load?.resume(returning: HouseholdMembershipPage(results: [context.member]))
        await loadTask.value

        XCTAssertTrue(context.viewModel.sections[0].members.isEmpty)
        XCTAssertFalse(context.viewModel.sections[0].isLoading)
    }

    func testMembershipLoadDuringFailedRemovalCannotOverrideSingleRollback() async {
        let context = makeRaceContext()
        var removal: CheckedContinuation<Void, Error>?
        var load: CheckedContinuation<HouseholdMembershipPage, Error>?
        context.service.removalHandler = { _, _ in
            try await withCheckedThrowingContinuation { removal = $0 }
        }
        context.service.membershipHandler = { _ in
            try await withCheckedThrowingContinuation { load = $0 }
        }
        let removeTask = Task {
            await context.viewModel.removeMembership(context.member, householdId: context.household.id)
        }
        await waitUntil { removal != nil }
        let loadTask = Task { await context.viewModel.loadMemberships(householdId: context.household.id) }
        await waitUntil { load != nil }
        load?.resume(returning: HouseholdMembershipPage(results: [context.member]))
        await loadTask.value
        removal?.resume(throwing: HouseholdTestError.failed)
        await removeTask.value

        XCTAssertEqual(context.viewModel.sections[0].members.map(\.id), [context.member.id])
    }

    private func makeRaceContext() -> (
        service: HouseholdServiceStub,
        household: Household,
        member: HouseholdMembership,
        viewModel: HouseholdViewModel
    ) {
        let service = HouseholdServiceStub()
        let household = makeHousehold(id: "owned", ownerId: "owner")
        let member = makeHouseholdMembership(id: "member", householdId: household.id)
        let viewModel = makeHouseholdViewModel(service: service)
        viewModel.sections = [section(household: household, members: [member])]
        return (service, household, member, viewModel)
    }

    private func section(
        household: Household,
        members: [HouseholdMembership]
    ) -> HouseholdMembershipSection {
        HouseholdMembershipSection(
            household: household,
            members: members,
            isLoading: false,
            errorMessage: nil,
            isOwned: true
        )
    }

    private func waitUntil(_ condition: () -> Bool) async {
        for _ in 0 ..< 100 where !condition() {
            await Task.yield()
        }
        XCTAssertTrue(condition())
    }
}
