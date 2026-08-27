@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdViewModelTests: XCTestCase {
    func testReloadReplacesTheFirstMembershipPage() async {
        let service = HouseholdServiceStub()
        let household = makeHousehold(id: "owned", ownerId: "owner")
        service.ownedHouseholdPage = HouseholdPage(results: [household])
        service.membershipPages[household.id] = HouseholdMembershipPage(results: [
            makeHouseholdMembership(id: "removed", householdId: household.id),
            makeHouseholdMembership(id: "retained", householdId: household.id, username: "before")
        ])
        let viewModel = makeHouseholdViewModel(service: service)
        await viewModel.load()

        service.membershipPages[household.id] = HouseholdMembershipPage(results: [
            makeHouseholdMembership(
                id: "retained",
                householdId: household.id,
                username: "after",
                relationship: "parent"
            )
        ])
        await viewModel.load()

        let member = try? XCTUnwrap(viewModel.sections.first?.members.first)
        XCTAssertEqual(viewModel.sections.first?.members.map(\.id), ["retained"])
        XCTAssertEqual(member?.individual.username, "after")
        XCTAssertEqual(member?.relationship, "parent")
    }

    func testLoadUsesIndependentOwnedProbeAndMemberPage() async {
        let service = HouseholdServiceStub()
        service.ownedHouseholdPage = HouseholdPage(results: [makeHousehold(id: "owned-first", ownerId: "owner")])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [
            makeHousehold(id: "member-1", ownerId: "other"),
            makeHousehold(id: "member-2", ownerId: "another")
        ])
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()

        XCTAssertEqual(viewModel.ownedHousehold?.id, "owned-first")
        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), ["member-1", "member-2"])
        XCTAssertEqual(viewModel.sections.map(\.id), ["owned-first", "member-1", "member-2"])
        XCTAssertFalse(viewModel.sections[1].isOwned)
    }

    func testMemberOnlyHouseholdsDoNotPreventCreation() async {
        let service = HouseholdServiceStub()
        service.ownedHouseholdPage = HouseholdPage(results: [])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [makeHousehold(id: "member", ownerId: "other")])
        service.createdHousehold = makeHousehold(id: "created", ownerId: "owner")
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        await viewModel.createHousehold()

        XCTAssertEqual(service.createCalls, 1)
        XCTAssertEqual(viewModel.ownedHousehold?.id, "created")
        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), ["member"])
        XCTAssertEqual(viewModel.sections.map(\.id), ["created", "member"])
    }

    func testCreationRequiresSuccessfulListAndRefreshFailureRevokesEligibility() async {
        let service = HouseholdServiceStub()
        service.ownedHouseholdPage = HouseholdPage(results: [])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [makeHousehold(id: "member", ownerId: "other")])
        service.createdHousehold = makeHousehold(id: "created", ownerId: "owner")
        service.ownedHouseholdError = HouseholdTestError.failed
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        XCTAssertFalse(viewModel.canCreateHousehold)
        await viewModel.createHousehold()
        XCTAssertEqual(service.createCalls, 0)

        service.ownedHouseholdError = nil
        await viewModel.load()
        XCTAssertTrue(viewModel.canCreateHousehold)

        service.ownedHouseholdError = HouseholdTestError.failed
        await viewModel.load()
        XCTAssertFalse(viewModel.canCreateHousehold)
        await viewModel.createHousehold()

        XCTAssertEqual(service.createCalls, 0)
    }

    func testMembershipLoadsPreserveSuccessfulSectionsAndRetryOnlyFailure() async {
        let service = HouseholdServiceStub()
        let owned = makeHousehold(id: "owned", ownerId: "owner")
        let memberOnly = makeHousehold(id: "member-only", ownerId: "other")
        service.ownedHouseholdPage = HouseholdPage(results: [owned])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [memberOnly])
        service.membershipPages[owned.id] = HouseholdMembershipPage(results: [
            makeHouseholdMembership(id: "success", householdId: owned.id)
        ])
        service.membershipErrors[memberOnly.id] = HouseholdTestError.failed
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["success"])
        XCTAssertNotNil(viewModel.sections[1].errorMessage)

        service.membershipErrors[memberOnly.id] = nil
        service.membershipPages[memberOnly.id] = HouseholdMembershipPage(results: [
            makeHouseholdMembership(id: "retried", householdId: memberOnly.id)
        ])
        await viewModel.loadNextMemberships(householdId: memberOnly.id)

        XCTAssertEqual(service.membershipCalls[owned.id], 1)
        XCTAssertEqual(service.membershipCalls[memberOnly.id], 2)
        XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["success"])
        XCTAssertEqual(viewModel.sections[1].members.map(\.id), ["retried"])
    }

    func testMemberHouseholdsAppendWithExactCursorAndDeduplicate() async {
        let service = HouseholdServiceStub()
        let first = makeHousehold(id: "member-first", ownerId: "other")
        let second = makeHousehold(id: "member-second", ownerId: "another")
        service.ownedHouseholdPage = HouseholdPage(results: [])
        service.memberHouseholdPages[nil] = HouseholdPage(
            results: [first],
            pageInfo: .init(hasNextPage: true, endCursor: "member-cursor")
        )
        service.memberHouseholdPages["member-cursor"] = HouseholdPage(results: [first, second])
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        await viewModel.loadNextMemberHouseholds()

        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), [first.id, second.id])
        XCTAssertEqual(viewModel.sections.map(\.id), [first.id, second.id])
        XCTAssertFalse(viewModel.hasMoreMemberHouseholds)
    }

    func testMemberContinuationFailurePreservesRowsAndRetriesSameCursor() async {
        let service = HouseholdServiceStub()
        let first = makeHousehold(id: "member-first", ownerId: "other")
        service.memberHouseholdPages[nil] = HouseholdPage(
            results: [first],
            pageInfo: .init(hasNextPage: true, endCursor: "member-cursor")
        )
        service.memberHouseholdErrors["member-cursor"] = HouseholdTestError.failed
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        await viewModel.loadNextMemberHouseholds()
        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), [first.id])
        XCTAssertTrue(viewModel.hasMemberHouseholdPaginationError)

        service.memberHouseholdErrors["member-cursor"] = nil
        service.memberHouseholdPages["member-cursor"] = HouseholdPage(results: [
            makeHousehold(id: "member-second", ownerId: "another")
        ])
        await viewModel.loadNextMemberHouseholds()

        XCTAssertEqual(viewModel.memberOnlyHouseholds.map(\.id), [first.id, "member-second"])
        XCTAssertFalse(viewModel.hasMemberHouseholdPaginationError)
    }

    func testMembershipPaginationIsIndependentPerHousehold() async {
        let firstHousehold = makeHousehold(id: "first", ownerId: "owner")
        let secondHousehold = makeHousehold(id: "second", ownerId: "other")
        let service = HouseholdServiceStub()
        service.ownedHouseholdPage = HouseholdPage(results: [firstHousehold])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [secondHousehold])
        service.paginatedMembershipHandler = { householdId, after in
            if householdId == firstHousehold.id, after == nil {
                return HouseholdMembershipPage(
                    results: [makeHouseholdMembership(id: "first-a", householdId: householdId)],
                    pageInfo: .init(hasNextPage: true, endCursor: "first-cursor")
                )
            }
            if householdId == firstHousehold.id, after == "first-cursor" {
                return HouseholdMembershipPage(results: [
                    makeHouseholdMembership(id: "first-b", householdId: householdId)
                ])
            }
            return HouseholdMembershipPage(results: [
                makeHouseholdMembership(id: "second-a", householdId: householdId)
            ])
        }
        let viewModel = makeHouseholdViewModel(service: service)

        await viewModel.load()
        await viewModel.loadNextMemberships(householdId: firstHousehold.id)

        XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["first-a", "first-b"])
        XCTAssertEqual(viewModel.sections[1].members.map(\.id), ["second-a"])
        XCTAssertEqual(service.membershipCalls[firstHousehold.id], 2)
        XCTAssertEqual(service.membershipCalls[secondHousehold.id], 1)
    }

    func testMembershipPaginationAllowsOnlyOneInFlightRequest() async {
        let service = HouseholdServiceStub()
        let household = makeHousehold(id: "owned", ownerId: "owner")
        var continuations: [CheckedContinuation<HouseholdMembershipPage, Error>] = []
        service.membershipHandler = { _ in
            try await withCheckedThrowingContinuation { continuations.append($0) }
        }
        let viewModel = makeHouseholdViewModel(service: service)
        viewModel.sections = [section(household: household)]

        let first = Task { await viewModel.loadMemberships(householdId: household.id) }
        await waitUntil { continuations.count == 1 }
        let retry = Task { await viewModel.loadNextMemberships(householdId: household.id) }
        await Task.yield()
        XCTAssertEqual(continuations.count, 1)
        continuations[0].resume(returning: HouseholdMembershipPage(results: [
            makeHouseholdMembership(id: "fresh", householdId: household.id)
        ]))
        await first.value
        await retry.value

        XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["fresh"])
    }

    func testRemovalDeduplicatesSameMemberAndAllowsConcurrentDifferentMembers() async {
        let service = HouseholdServiceStub()
        let household = makeHousehold(id: "owned", ownerId: "owner")
        let firstMember = makeHouseholdMembership(id: "first", householdId: household.id)
        let secondMember = makeHouseholdMembership(id: "second", householdId: household.id)
        var continuations: [String: CheckedContinuation<Void, Error>] = [:]
        service.removalHandler = { _, membershipId in
            try await withCheckedThrowingContinuation { continuations[membershipId] = $0 }
        }
        let viewModel = makeHouseholdViewModel(service: service)
        viewModel.sections = [section(household: household, members: [firstMember, secondMember])]

        let first = Task { await viewModel.removeMembership(firstMember, householdId: household.id) }
        let duplicate = Task { await viewModel.removeMembership(firstMember, householdId: household.id) }
        let second = Task { await viewModel.removeMembership(secondMember, householdId: household.id) }
        await waitUntil { continuations.count == 2 }

        XCTAssertEqual(service.removalCalls["first"], 1)
        XCTAssertEqual(service.removalCalls["second"], 1)
        XCTAssertTrue(viewModel.sections[0].members.isEmpty)
        continuations.values.forEach { $0.resume() }
        await first.value
        await duplicate.value
        await second.value
    }

    func testFailedRemovalRollsBackWithoutDuplicatesAfterOtherMembersDisappear() async {
        let service = HouseholdServiceStub()
        let household = makeHousehold(id: "owned", ownerId: "owner")
        let first = makeHouseholdMembership(id: "first", householdId: household.id)
        let removed = makeHouseholdMembership(id: "removed", householdId: household.id)
        var continuation: CheckedContinuation<Void, Error>?
        service.removalHandler = { _, _ in
            try await withCheckedThrowingContinuation { continuation = $0 }
        }
        let viewModel = makeHouseholdViewModel(service: service)
        viewModel.sections = [section(household: household, members: [first, removed])]

        let task = Task { await viewModel.removeMembership(removed, householdId: household.id) }
        await waitUntil { continuation != nil }
        viewModel.sections[0].members = []
        continuation?.resume(throwing: HouseholdTestError.failed)
        await task.value

        XCTAssertEqual(viewModel.sections[0].members.map(\.id), ["removed"])
        XCTAssertEqual(Set(viewModel.sections[0].members.map(\.id)).count, viewModel.sections[0].members.count)
    }

    func testMemberPresentationNeverFallsBackToIdentifiers() {
        let named = makeHouseholdMembership(id: "membership-id", householdId: "household-id")
        let unnamed = makeHouseholdMembership(
            id: "00000000-0000-7000-8000-000000000201",
            householdId: "00000000-0000-7000-8000-000000000101",
            username: nil,
            relationship: " "
        )

        XCTAssertEqual(named.householdDisplayName, "@alice")
        XCTAssertEqual(named.householdDisplayRelationship, "spouse")
        XCTAssertEqual(unnamed.householdDisplayName, "Household member")
        XCTAssertNil(unnamed.householdDisplayRelationship)
    }

    func testApplyingMembersKeepsTheFirstRankForDuplicateMembershipIds() {
        let household = makeHousehold(id: "owned", ownerId: "owner")
        let first = makeHouseholdMembership(id: "duplicate", householdId: household.id)
        let second = makeHouseholdMembership(id: "second", householdId: household.id)
        var subject = section(household: household)

        subject.applyMembers([first, second, first])

        XCTAssertEqual(subject.members.map(\.id), ["duplicate", "second"])
        XCTAssertEqual(subject.members.first, first)
        XCTAssertEqual(subject.memberOrder, ["duplicate": 0, "second": 1])

        subject.applyMembers([first, second, first], excluding: [first.id])
        subject.restore(first, fallbackIndex: 1)

        XCTAssertEqual(subject.members.map(\.id), ["duplicate", "second"])
        XCTAssertEqual(subject.memberOrder, ["duplicate": 0, "second": 1])
    }

    private func section(
        household: Household,
        members: [HouseholdMembership] = []
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
