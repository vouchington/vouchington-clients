import ViewInspector
import VouchaAPI
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class HouseholdSurfaceTests: XCTestCase {
    func testOwnedAndMemberOnlySectionsRenderNativeLabelsAndReadOnlyActions() throws {
        let owned = makeHousehold(id: "owned", ownerId: "owner")
        let memberOnly = makeHousehold(id: "member-only", ownerId: "other")
        let viewModel = makeHouseholdViewModel()
        viewModel.ownedHousehold = owned
        setLoadedMemberHouseholds([memberOnly], on: viewModel)
        viewModel.sections = [
            makeSection(
                household: owned,
                members: [makeHouseholdMembership(id: "owned-member", householdId: owned.id)],
                isOwned: true
            ),
            makeSection(
                household: memberOnly,
                members: [makeHouseholdMembership(
                    id: "00000000-0000-7000-8000-000000000201",
                    householdId: memberOnly.id,
                    username: nil,
                    relationship: " "
                )],
                isOwned: false
            )
        ]
        let sut = HouseholdSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "@alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Household member"))
        XCTAssertNoThrow(try sut.inspect().find(text: "spouse"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Read only"))
        XCTAssertThrowsError(try sut.inspect().find(text: "00000000-0000-7000-8000-000000000201"))
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Button.self).count, 1)
    }

    func testMembershipErrorRendersRetryWithoutHidingSuccessfulSection() throws {
        let owned = makeHousehold(id: "owned", ownerId: "owner")
        let other = makeHousehold(id: "other", ownerId: "other")
        let viewModel = makeHouseholdViewModel()
        viewModel.ownedHousehold = owned
        setLoadedMemberHouseholds([other], on: viewModel)
        viewModel.sections = [
            makeSection(
                household: owned,
                members: [makeHouseholdMembership(id: "visible", householdId: owned.id)],
                isOwned: true
            ),
            HouseholdMembershipSection(
                household: other,
                members: [],
                isLoading: false,
                errorMessage: .verbatim("Members failed"),
                isOwned: false
            )
        ]
        let sut = HouseholdSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "@alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Members failed"))
        let controls = try sut.inspect().findAll(HouseholdMembershipSectionView.self)
        XCTAssertNoThrow(try controls[1].find(HybridPaginationControl.self).actualView().inspect()
            .find(button: "Try Again"))
    }

    func testEmptyOwnedStateOffersCreateEvenWithMemberOnlyHouseholds() async throws {
        let household = makeHousehold(id: "member-only", ownerId: "other")
        let service = HouseholdServiceStub()
        service.ownedHouseholdPage = HouseholdPage(results: [])
        service.memberHouseholdPages[nil] = HouseholdPage(results: [household])
        let viewModel = makeHouseholdViewModel(service: service)
        await viewModel.load()
        let sut = HouseholdSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Household you belong to"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Create household"))
    }

    func testForwardHouseholdAndMembershipPagesRenderHybridControls() throws {
        let owned = makeHousehold(id: "owned", ownerId: "owner")
        let viewModel = makeHouseholdViewModel()
        viewModel.ownedHousehold = owned
        var householdPagination = CursorPaginationState<Household>()
        let householdRequest = try XCTUnwrap(householdPagination.beginInitialPageIfNeeded())
        householdPagination.complete(
            householdRequest,
            items: [makeHousehold(id: "member", ownerId: "other")],
            endCursor: "household-next",
            hasNextPage: true
        )
        viewModel.memberHouseholdPagination = householdPagination
        var ownedSection = makeSection(household: owned, members: [], isOwned: true)
        let membershipRequest = try XCTUnwrap(ownedSection.beginInitialPageIfNeeded())
        ownedSection.complete(
            membershipRequest,
            page: HouseholdMembershipPage(
                results: [makeHouseholdMembership(id: "first", householdId: owned.id)],
                pageInfo: .init(hasNextPage: true, endCursor: "membership-next")
            ),
            excluding: []
        )
        viewModel.sections = [ownedSection]
        let sut = HouseholdSurface(viewModel: viewModel)

        XCTAssertGreaterThanOrEqual(try sut.inspect().findAll(ViewType.Button.self).count, 2)
    }

    private func makeSection(
        household: Household,
        members: [HouseholdMembership],
        isOwned: Bool
    ) -> HouseholdMembershipSection {
        HouseholdMembershipSection(
            household: household,
            members: members,
            isLoading: false,
            errorMessage: nil,
            isOwned: isOwned
        )
    }
}
