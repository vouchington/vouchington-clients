import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ModerationAppealsViewModelTests: NativeRouteSurfaceViewModelTestCase {

    func testLoadsFiltersAndPaginatesWithoutDuplicates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(id: "appeal-1")], hasNextPage: true, endCursor: "cursor-1"), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(id: "appeal-1"), Support.appeal(id: "appeal-2")]), 200
        )
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.appeals.map(\.id), ["appeal-1", "appeal-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[0].query, "limit=25&status=pending")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[1].query, "limit=25&status=pending&after=cursor-1")
        XCTAssertFalse(viewModel.hasMore)

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(id: "appeal-3", status: "dismissed")]), 200
        )
        await viewModel.selectStatus(.dismissed)
        XCTAssertEqual(viewModel.appeals.map(\.id), ["appeal-3"])
    }

    func testIgnoresStaleFilterResponseAndPreservesRowsWhenLoadMoreFails() async throws {
        let path = "/api/v1/appeals"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Support.list([Support.appeal(id: "stale")]), 200, 0),
            (Support.list([Support.appeal(id: "resolved", status: "resolved")], hasNextPage: true), 200, 0)
        ]
        let viewModel = try staffViewModel()

        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let staleLoad = Task { await viewModel.load() }
        guard await Support.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await staleLoad.value
            return
        }

        let resolvedLoad = Task { await viewModel.selectStatus(.resolved) }
        guard await Support.waitForCapturedCannedFeedRequests(path: path, minimumCount: 2) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await resolvedLoad.value
            await staleLoad.value
            return
        }
        CannedFeedURLProtocol.releaseResponse(path: path)

        await resolvedLoad.value
        await staleLoad.value

        XCTAssertEqual(viewModel.appeals.map(\.id), ["resolved"])
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"failed"}"#.utf8), 500)
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.appeals.map(\.id), ["resolved"])
        XCTAssertNotNil(viewModel.loadMoreErrorMessage)
    }

    func testReloadClearsStaleLoadMoreState() async throws {
        let path = "/api/v1/appeals"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (Support.list([Support.appeal()], hasNextPage: true, endCursor: "next"), 200, 0),
            (Support.list([Support.appeal(id: "stale-page")]), 200, 0),
            (Support.list([Support.appeal(id: "fresh")]), 200, 0)
        ]
        let viewModel = try staffViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let staleLoadMore = Task { await viewModel.loadMore() }
        guard await Support.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await staleLoadMore.value
            return
        }

        let reload = Task { await viewModel.load() }
        guard await Support.waitForCapturedCannedFeedRequests(path: path, minimumCount: 3) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await reload.value
            await staleLoadMore.value
            return
        }
        CannedFeedURLProtocol.releaseResponse(path: path)

        await reload.value
        await staleLoadMore.value

        XCTAssertEqual(viewModel.appeals.map(\.id), ["fresh"])
        XCTAssertFalse(viewModel.isLoadingMore)
    }

    func testSavesChangedDraftAndSkipsNoOpPatch() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        viewModel.setDraft("Human response", for: viewModel.appeals[0])
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal(publicResponse: "Human response")), 200
        )

        let didSave = await viewModel.saveDraft(for: viewModel.appeals[0])
        XCTAssertTrue(didSave)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "PATCH")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.last?.contains("Human response") == true)
        let requestCount = CannedFeedURLProtocol.capturedURLs.count
        let didSkipNoOp = await viewModel.saveDraft(for: viewModel.appeals[0])
        XCTAssertTrue(didSkipNoOp)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requestCount)
    }

    func testReloadRefreshesCleanDraftButPreservesDirtyDraftAcrossStatusSwitches() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(aiPublicResponse: "Initial AI draft")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Initial AI draft")

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(aiPublicResponse: "Fresh AI draft")]), 200
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Fresh AI draft")

        viewModel.setDraft("Local edit", for: viewModel.appeals[0])
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(aiPublicResponse: "Newer AI draft")]), 200
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Local edit")

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(id: "resolved", status: "resolved")]), 200
        )
        await viewModel.selectStatus(.resolved)
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(aiPublicResponse: "Newest AI draft")]), 200
        )
        await viewModel.selectStatus(.pending)
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Local edit")
    }

    func testServerReplacementClearsDirtyDraftAndAdoptsFutureServerSeeds() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(aiPublicResponse: "AI draft")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        viewModel.setDraft("Local edit", for: viewModel.appeals[0])
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal(publicResponse: "Confirmed response")), 200
        )

        let didSave = await viewModel.saveDraft(for: viewModel.appeals[0])
        XCTAssertTrue(didSave)
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Confirmed response")

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(publicResponse: "Later server response")]), 200
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Later server response")
    }

    func testNoOpSaveConfirmsDirtyDraftAndAllowsLaterServerRefresh() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(publicResponse: "Initial response")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        viewModel.setDraft("Server caught up", for: viewModel.appeals[0])

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(publicResponse: "Server caught up")]), 200
        )
        await viewModel.load()
        let requestCount = CannedFeedURLProtocol.capturedURLs.count
        let didSave = await viewModel.saveDraft(for: viewModel.appeals[0])
        XCTAssertTrue(didSave)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requestCount)

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(publicResponse: "Later server response")]), 200
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Later server response")
    }

    func testApproveSavesBeforeApprovalAndRetainsDraftWhenApprovalFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(publicResponse: "Old response")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        viewModel.setDraft("Revised response", for: viewModel.appeals[0])
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal(publicResponse: "Revised response")), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1/approval"] = (Data(), 500)

        await viewModel.approve(viewModel.appeals[0])

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.suffix(2), ["PATCH", "POST"])
        XCTAssertEqual(viewModel.appeals[0].publicResponse, "Revised response")
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Revised response")
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testSendRequiresTheLocalDraftToMatchTheServerConfirmedResponse() async throws {
        let approved = Support.appeal(
            publicResponse: "Server response", approvedAt: "2026-07-01T11:00:00Z"
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([approved]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)

        XCTAssertTrue(viewModel.canSend(appeal))
        viewModel.setDraft("Unsaved visible edit", for: appeal)
        XCTAssertFalse(viewModel.canSend(appeal))

        await viewModel.send(appeal)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.hasSuffix("/delivery") })
    }

    func testSendResolveAndAIRerunUseServerConfirmedRows() async throws {
        let approved = Support.appeal(
            id: "appeal-send", publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([
                approved,
                Support.appeal(id: "appeal-resolve", sentAt: "2026-07-01T12:00:00Z"),
                Support.appeal(id: "appeal-ai")
            ]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-send/delivery"] = (
            Support.envelope(Support.appeal(
                id: "appeal-send", publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z",
                sentAt: "2026-07-01T12:00:00Z"
            )), 200
        )
        try await viewModel.send(XCTUnwrap(viewModel.appeals.first { $0.id == "appeal-send" }))
        XCTAssertNotNil(viewModel.appeals.first { $0.id == "appeal-send" }?.sentAt)

        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-resolve/resolution"] = (
            Support.envelope(Support.appeal(id: "appeal-resolve", status: "resolved", resolutionAction: "deny")), 200
        )
        try await viewModel.resolve(
            XCTUnwrap(viewModel.appeals.first { $0.id == "appeal-resolve" }), action: .deny
        )
        XCTAssertNil(viewModel.appeals.first { $0.id == "appeal-resolve" })

        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-ai/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals/appeal-ai"] = [
            (Support.envelope(Support.appeal(id: "appeal-ai")), 200, 0),
            (Support.envelope(Support.appeal(
                id: "appeal-ai",
                aiPublicResponse: "Fresh AI draft",
                aiDraftedAt: "2026-07-01T10:01:00Z",
                lifecycleChangeId: "change-2"
            )), 200, 0)
        ]
        let pollingViewModel = try staffViewModel(rerunPollDelay: {})
        pollingViewModel.appeals = viewModel.appeals
        try await pollingViewModel.rerunAI(
            for: XCTUnwrap(pollingViewModel.appeals.first { $0.id == "appeal-ai" })
        )
        XCTAssertEqual(
            pollingViewModel.appeals.first { $0.id == "appeal-ai" }?.aiPublicResponse,
            "Fresh AI draft"
        )
    }

    func testAIRerunTimeoutPreservesExistingRowAndLocalDraft() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel(rerunPollAttempts: 2, rerunPollDelay: {})
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        viewModel.setDraft("Local edit", for: appeal)
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal()), 200
        )

        await viewModel.rerunAI(for: appeal)

        XCTAssertEqual(viewModel.appeals.first?.aiPublicResponse, "AI draft")
        XCTAssertEqual(viewModel.drafts["appeal-1"], "Local edit")
        XCTAssertEqual(
            viewModel.errorMessage,
            "The AI draft is still processing. Refresh to check again."
        )
    }

    func testAIRerunCancellationStopsPollingAndPreservesExistingRow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel(rerunPollDelay: {
            try await Task.sleep(for: .seconds(30))
        })
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal()), 200
        )
        let rerun = Task { await viewModel.rerunAI(for: appeal) }
        for _ in 0 ..< 100
            where CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals/appeal-1") == 0 {
            await Task.yield()
        }

        rerun.cancel()
        await rerun.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals/appeal-1"), 1)
        XCTAssertEqual(viewModel.appeals.first?.aiPublicResponse, "AI draft")
        XCTAssertFalse(viewModel.isMutating)
    }

    func testApprovedAppealCannotRerunAI() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(approvedAt: "2026-07-01T11:00:00Z")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)

        XCTAssertFalse(viewModel.canRerun(appeal))
        await viewModel.rerunAI(for: appeal)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path.hasSuffix("/resolution-drafts")
        })
    }

    func testResolutionRequiresSentAppealAndUsesServerConfirmedRow() async throws {
        let unsent = Support.appeal(id: "appeal-unsent")
        let sent = Support.appeal(id: "appeal-sent", sentAt: "2026-07-01T12:00:00Z")
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([unsent, sent]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()

        let loadedUnsent = try XCTUnwrap(viewModel.appeals.first { $0.id == "appeal-unsent" })
        XCTAssertFalse(viewModel.canResolve(loadedUnsent, action: .deny))
        await viewModel.resolve(loadedUnsent, action: .deny)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/appeals/appeal-unsent/resolution"
        })

        let loadedSent = try XCTUnwrap(viewModel.appeals.first { $0.id == "appeal-sent" })
        XCTAssertTrue(viewModel.canResolve(loadedSent, action: .deny))
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-sent/resolution"] = (
            Support.envelope(Support.appeal(
                id: "appeal-sent", status: "resolved", sentAt: "2026-07-01T12:00:00Z",
                resolutionAction: "deny"
            )), 200
        )
        await viewModel.resolve(loadedSent, action: .deny)

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/appeals/appeal-sent/resolution"
        })
        XCTAssertNil(viewModel.appeals.first { $0.id == "appeal-sent" })
    }

    func testGlobalMutationGuardAndModeratorSuspensionRestriction() async throws {
        let suspended = Support.appeal(
            id: "appeal-suspension",
            suspensionId: "suspension-1",
            sentAt: "2026-07-01T12:00:00Z"
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(), suspended]), 200
        )
        let moderator = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: false, isSiteModerator: true
        )
        await moderator.load()
        let suspensionAppeal = try XCTUnwrap(moderator.appeals.first { $0.id == "appeal-suspension" })
        XCTAssertFalse(moderator.canResolve(suspensionAppeal, action: .accept))
        XCTAssertTrue(moderator.canResolve(suspensionAppeal, action: .reduce))

        let first = moderator.appeals[0]
        moderator.setDraft("Changed", for: first)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals/appeal-1"] = [
            (Support.envelope(Support.appeal(publicResponse: "Changed")), 200, 0.1)
        ]
        let save = Task { await moderator.saveDraft(for: first) }
        await Task.yield()
        await moderator.resolve(suspensionAppeal, action: .reduce)
        _ = await save.value
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.hasSuffix("/resolution") })

        let administrator = try staffViewModel()
        administrator.appeals = [suspensionAppeal]
        XCTAssertTrue(administrator.canResolve(suspensionAppeal, action: .accept))
    }

    func testUnauthorizedRolesNeverRequestAppeals() async throws {
        let member = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: false, isSiteModerator: false
        )
        let anonymous = ModerationAppealsViewModel(
            client: nil, isSignedIn: false, isAdministrator: false, isSiteModerator: false
        )
        await member.load()
        await anonymous.load()
        XCTAssertFalse(member.canAccess)
        XCTAssertFalse(anonymous.canAccess)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testAmbiguousDeliveryRefreshesOnlyTheAffectedAppealById() async throws {
        let approved = Support.appeal(
            id: "appeal-send", publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([approved, Support.appeal(id: "appeal-other")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.errors["/api/v1/appeals/appeal-send/delivery"] = URLError(.networkConnectionLost)

        try await viewModel.send(XCTUnwrap(viewModel.appeals.first { $0.id == "appeal-send" }))
        XCTAssertTrue(viewModel.ambiguousDeliveryAppealIds.contains("appeal-send"))

        CannedFeedURLProtocol.errors.removeValue(forKey: "/api/v1/appeals/appeal-send/delivery")
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-send"] = (
            Support.envelope(Support.appeal(
                id: "appeal-send", publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z",
                sentAt: "2026-07-01T12:00:00Z"
            )), 200
        )
        await viewModel.clearAmbiguousDelivery(for: "appeal-send")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/appeals/appeal-send")
        XCTAssertEqual(viewModel.appeals.map(\.id), ["appeal-send", "appeal-other"])
        XCTAssertNotNil(viewModel.appeals.first?.sentAt)
        XCTAssertFalse(viewModel.ambiguousDeliveryAppealIds.contains("appeal-send"))
    }

    func testAmbiguousDeliveryStaysAmbiguousWhenConfirmationFails() async throws {
        let approved = Support.appeal(
            id: "appeal-send", publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([approved]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.errors["/api/v1/appeals/appeal-send/delivery"] = URLError(.networkConnectionLost)
        await viewModel.send(viewModel.appeals[0])

        CannedFeedURLProtocol.errors.removeValue(forKey: "/api/v1/appeals/appeal-send/delivery")
        CannedFeedURLProtocol.errors["/api/v1/appeals/appeal-send"] = URLError(.notConnectedToInternet)
        await viewModel.clearAmbiguousDelivery(for: "appeal-send")

        XCTAssertTrue(viewModel.ambiguousDeliveryAppealIds.contains("appeal-send"))
        XCTAssertNotNil(viewModel.errorMessage)
    }

    private typealias Support = ModerationAppealsTestSupport

    private func staffViewModel(
        rerunPollAttempts: Int = 20,
        rerunPollDelay: @escaping @Sendable () async throws -> Void = {}
    ) throws -> ModerationAppealsViewModel {
        try ModerationAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: rerunPollAttempts,
            rerunPollDelay: rerunPollDelay
        )
    }
}
