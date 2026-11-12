import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdInvitationsViewModelTests: XCTestCase {
    func testReceivedPagesForwardCursorDeduplicateAndIgnoreTerminalInvitations() async throws {
        let first = try invitationFixture()
        let second = try invitationFixture(edits: ["id": "second"])
        let terminal = try invitationFixture(edits: ["id": "terminal", "accepted_at": "2026-01-01T00:00:00.000Z"])
        let service = InvitationServiceStub()
        service.pages[nil] = HouseholdInvitationPage(
            results: [first],
            pageInfo: .init(hasNextPage: true, endCursor: "opaque")
        )
        service.pages["opaque"] = HouseholdInvitationPage(results: [first, second, terminal])
        let model = HouseholdInvitationsViewModel(service: service)
        await model.load()
        await model.load()
        await model.loadNext()
        await model.loadNext()
        XCTAssertEqual(model.invitations.map(\.id), [first.id, second.id])
        XCTAssertEqual(service.requests.count, 2)
        XCTAssertEqual(service.requests.last?.1, "opaque")
        XCTAssertEqual(service.requests.first?.2, 25)
        XCTAssertNil(service.requests.first?.0)
    }

    func testSendTrimsInputsReloadsOwnedListAndReportsSuccess() async throws {
        let service = InvitationServiceStub()
        service.response = try invitationFixture()
        service.pages[nil] = try HouseholdInvitationPage(results: [invitationFixture()])
        let model = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        model.username = " alice \n"
        model.relationship = " partner "
        await model.send()
        XCTAssertEqual(service.sentArguments?.0, "owned")
        XCTAssertEqual(service.sentArguments?.1, "alice")
        XCTAssertEqual(service.sentArguments?.2, "partner")
        XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationSent)
        XCTAssertEqual(model.username, "")
        XCTAssertEqual(model.relationship, "")
        XCTAssertEqual(model.invitations.count, 1)
        XCTAssertEqual(service.requests.first?.0, "owned")
        model.username = "bob"
        model.relationship = " "
        await model.send()
        XCTAssertNil(service.sentArguments?.2)
    }

    func testDuplicateInviteReturnedOutsideFirstPageRemainsVisible() async throws {
        let service = InvitationServiceStub()
        let older = try invitationFixture(edits: ["id": "older"])
        let newer = try invitationFixture(edits: ["id": "newer"])
        service.response = older
        service.pages[nil] = HouseholdInvitationPage(
            results: [newer],
            pageInfo: .init(hasNextPage: true, endCursor: "cursor")
        )
        let model = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        model.username = "alice"
        await model.send()
        XCTAssertEqual(model.invitations.map(\.id), [older.id, newer.id])
        XCTAssertEqual(model.pagination.endCursor, "cursor")
        XCTAssertTrue(model.pagination.hasMore)
    }

    func testInvalidOrBusySendDoesNotCallServiceAndFailureKeepsDraft() async {
        let service = InvitationServiceStub()
        let received = HouseholdInvitationsViewModel(service: service)
        received.username = "alice"
        await received.send()
        let owned = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        await owned.send()
        owned.username = "alice"
        owned.isSending = true
        await owned.send()
        XCTAssertNil(service.sentArguments)
        owned.isSending = false
        service.error = VouchaError.api(statusCode: 404, preconditionCode: nil)
        await owned.send()
        XCTAssertEqual(owned.message, .settingsHouseholdInvitationsInvitationSendFailed)
        XCTAssertEqual(owned.username, "alice")
        XCTAssertFalse(owned.isSending)
    }

    func testAcceptDeclineRevokeRemoveOnlyAfterSuccess() async throws {
        for action in [HouseholdInvitationsViewModel.Action.accept, .decline, .revoke] {
            let service = InvitationServiceStub()
            service.membership = makeHouseholdMembership(id: "membership", householdId: "owned")
            let invitation = try invitationFixture()
            let model = HouseholdInvitationsViewModel(service: service, householdId: action == .revoke ? "owned" : nil)
            model.pagination = CursorPaginationState(items: [invitation])
            await model.update(invitation, action: action)
            XCTAssertTrue(model.invitations.isEmpty)
            XCTAssertTrue(model.busyIds.isEmpty)
            await model.update(invitation, action: action)
            XCTAssertEqual(service.actions.count, 1)
            switch action {
            case .accept:
                XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationAccepted)
                XCTAssertEqual(service.actions, ["accept:\(invitation.id)"])
            case .decline:
                XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationDeclined)
                XCTAssertEqual(service.actions, ["decline:\(invitation.id)"])
            case .revoke:
                XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationRevoked)
                XCTAssertEqual(service.actions, ["revoke:owned:\(invitation.id)"])
            }
        }
    }

    func testMutationFailurePreservesInvitationAndAllowsRetry() async throws {
        let service = InvitationServiceStub()
        service.error = VouchaError.api(statusCode: 403, preconditionCode: nil)
        let invitation = try invitationFixture()
        let model = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        model.pagination = CursorPaginationState(items: [invitation])
        model.busyIds.insert(invitation.id)
        await model.update(invitation, action: .revoke)
        XCTAssertTrue(service.actions.isEmpty)
        model.busyIds = []
        await model.update(invitation, action: .revoke)
        XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationUpdateFailed)
        XCTAssertEqual(model.invitations.map(\.id), [invitation.id])
        service.error = nil
        await model.update(invitation, action: .revoke)
        XCTAssertTrue(model.invitations.isEmpty)
        let received = HouseholdInvitationsViewModel(service: service)
        await received.update(invitation, action: .revoke)
        XCTAssertNil(received.message)
        received.pagination = CursorPaginationState(items: [invitation])
        await received.update(invitation, action: .revoke)
        XCTAssertEqual(service.actions.count, 2)
        let sent = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        sent.pagination = CursorPaginationState(items: [invitation])
        await sent.update(invitation, action: .accept)
        await sent.update(invitation, action: .decline)
        XCTAssertEqual(service.actions.count, 2)
    }

    func testAcceptanceLimitFailuresKeepInvitationAvailableForRetry() async throws {
        let invitation = try invitationFixture()
        for code in [
            "HOUSEHOLD_LIMIT_REACHED",
            "HOUSEHOLD_MEMBER_LIMIT_REACHED",
            "INDIVIDUAL_HOUSEHOLD_LIMIT_REACHED"
        ] {
            let service = InvitationServiceStub()
            service.error = VouchaError.api(statusCode: 409, preconditionCode: code)
            service.membership = makeHouseholdMembership(id: "accepted", householdId: invitation.householdId)
            let model = HouseholdInvitationsViewModel(service: service)
            model.pagination = CursorPaginationState(items: [invitation])
            await model.update(invitation, action: .accept)
            XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationUpdateFailed)
            XCTAssertEqual(model.invitations.map(\.id), [invitation.id])
            XCTAssertTrue(model.busyIds.isEmpty)
            service.error = nil
            await model.update(invitation, action: .accept)
            XCTAssertEqual(model.message, .settingsHouseholdInvitationsInvitationAccepted)
            XCTAssertTrue(model.invitations.isEmpty)
        }
    }

    func testLoadFailureRetriesAndCancellationDoesNotShowError() async {
        let service = InvitationServiceStub()
        service.error = VouchaError.api(statusCode: 403, preconditionCode: nil)
        let model = HouseholdInvitationsViewModel(service: service, householdId: "owned")
        await model.load()
        XCTAssertNotNil(model.pagination.lastError)
        service.error = nil
        await model.loadNext()
        XCTAssertNil(model.pagination.lastError)
        XCTAssertTrue(model.pagination.hasLoadedPage)
        let cancelled = HouseholdInvitationsViewModel(service: service)
        service.onLoad = { throw CancellationError() }
        let task = Task { await cancelled.load() }
        task.cancel()
        await task.value
        XCTAssertNil(cancelled.pagination.lastError)
        XCTAssertFalse(cancelled.pagination.isLoading)
    }
}
