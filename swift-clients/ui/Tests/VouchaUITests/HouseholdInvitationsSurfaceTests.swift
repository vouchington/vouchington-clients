import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdInvitationsSurfaceTests: XCTestCase {
    func testSentFormShowsUsernameRelationshipAndInvitationIdentity() throws {
        let model = HouseholdInvitationsViewModel(service: InvitationServiceStub(), householdId: "owned")
        model.pagination = try CursorPaginationState(items: [invitationFixture()])
        let surface = HouseholdInvitationsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.inspect().find(text: "Invitation for @household-invitee"))
        XCTAssertNoThrow(try surface.inspect().find(text: "partner"))
        XCTAssertNoThrow(try surface.inspect().find(button: "Revoke invitation"))
        XCTAssertEqual(try surface.inspect().findAll(ViewType.TextField.self).count, 2)
        XCTAssertTrue(try surface.inspect().find(button: "Send invitation").isDisabled())
        try surface.inspect().findAll(ViewType.TextField.self)[0].setInput("alice")
        try surface.inspect().findAll(ViewType.TextField.self)[1].setInput("partner")
        XCTAssertEqual(model.username, "alice")
        XCTAssertEqual(model.relationship, "partner")
        XCTAssertFalse(try surface.inspect().find(button: "Send invitation").isDisabled())
        XCTAssertThrowsError(try surface.inspect().find(button: "Accept"))
    }

    func testReceivedOwnerAndOtherInviterUseUsernamesAndFallbackNeverIds() throws {
        let ownerInvitation = try invitationFixture()
        let otherInvitation = try invitationFixture(edits: [
            "id": "other", "invited_by_id": "admin-id",
            "invited_by": ["id": "admin-id", "username": "admin"],
            "household_owner": ["id": "private-owner-id", "username": NSNull()],
            "relationship": NSNull()
        ])
        let model = HouseholdInvitationsViewModel(service: InvitationServiceStub())
        model.pagination = CursorPaginationState(items: [ownerInvitation, otherInvitation])
        let surface = HouseholdInvitationsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.inspect().find(text: "Invited by @household-owner"))
        XCTAssertNoThrow(try surface.inspect().find(text: "Invited by @admin to A Voucha member's household"))
        XCTAssertThrowsError(try surface.inspect().find(text: "private-owner-id"))
        XCTAssertNoThrow(try surface.inspect().find(button: "Accept"))
        XCTAssertNoThrow(try surface.inspect().find(button: "Decline"))
        XCTAssertThrowsError(try surface.inspect().find(ViewType.TextField.self))
        XCTAssertThrowsError(try surface.inspect().find(button: "Revoke invitation"))
    }

    func testEmptyLoadingErrorAndMutationFeedbackRender() async throws {
        let service = InvitationServiceStub()
        let model = HouseholdInvitationsViewModel(service: service)
        let surface = HouseholdInvitationsSurface(viewModel: model)
        try await surface.inspect().vStack().callTask()
        XCTAssertNoThrow(try surface.inspect().find(text: "No pending invitations."))
        model.pagination.reset()
        _ = model.pagination.beginNextPage()
        XCTAssertNoThrow(try surface.inspect().find(ViewType.ProgressView.self))
        model.pagination.reset()
        service.error = HouseholdTestError.failed
        await model.load()
        XCTAssertNoThrow(try surface.inspect().find(button: "Retry"))
        model.message = .settingsHouseholdInvitationsInvitationUpdateFailed
        XCTAssertNoThrow(try surface.inspect().find(text: "The invitation could not be updated."))
    }

    func testRenderedAcceptDeclineAndRevokeActionsDispatch() async throws {
        for action in ["Accept", "Decline", "Revoke invitation"] {
            let service = InvitationServiceStub()
            service.membership = makeHouseholdMembership(id: "new-member", householdId: "owned")
            let invitation = try invitationFixture()
            let model = HouseholdInvitationsViewModel(
                service: service,
                householdId: action == "Revoke invitation" ? "owned" : nil
            )
            model.pagination = CursorPaginationState(items: [invitation])
            let completed = expectation(description: "Invitation action dispatched")
            service.onUpdate = { completed.fulfill() }
            var accepted = false
            let surface = HouseholdInvitationsSurface(viewModel: model, onAccepted: { accepted = true })
            try surface.inspect().find(button: action).tap()
            await fulfillment(of: [completed], timeout: 2)
            XCTAssertEqual(service.actions.count, 1)
            XCTAssertTrue(model.invitations.isEmpty)
            XCTAssertTrue(model.busyIds.isEmpty)
            XCTAssertEqual(accepted, action == "Accept")
        }
    }

    func testRenderedSendRetryAndLoadMoreDispatch() async throws {
        let service = InvitationServiceStub()
        service.response = try invitationFixture()
        let model = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        model.username = "alice"
        let surface = HouseholdInvitationsSurface(viewModel: model)
        let sent = expectation(description: "Sent invitation list reloaded")
        service.onLoad = {
            sent.fulfill()
            return HouseholdInvitationPage(results: [])
        }
        try surface.inspect().find(button: "Send invitation").tap()
        await fulfillment(of: [sent], timeout: 2)
        XCTAssertEqual(service.sentArguments?.1, "alice")
        service.onLoad = nil
        model.pagination.reset()
        service.error = HouseholdTestError.failed
        await model.load()
        let retried = expectation(description: "List retried")
        service.onLoad = {
            retried.fulfill()
            return HouseholdInvitationPage(results: [], pageInfo: .init(hasNextPage: true, endCursor: "next"))
        }
        try surface.inspect().find(button: "Retry").tap()
        await fulfillment(of: [retried], timeout: 2)
        XCTAssertNil(model.pagination.lastError)
        let more = expectation(description: "Next page loaded")
        service.onLoad = {
            more.fulfill()
            return HouseholdInvitationPage(results: [])
        }
        try surface.inspect().find(button: "Load more invitations").tap()
        await fulfillment(of: [more], timeout: 2)
        XCTAssertEqual(service.requests.last?.1, "next")
    }
}
