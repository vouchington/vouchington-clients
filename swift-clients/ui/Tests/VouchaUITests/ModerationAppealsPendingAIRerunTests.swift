import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealsPendingAIRerunTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ModerationAppealsTestSupport

    func testTimeoutAndReadFailuresKeepOnePostUntilBothResultMarkersAdvance() async throws {
        let path = "/api/v1/appeals/appeal-1"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        viewModel.setDraft("Local edit", for: appeal)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal()), 200)

        await viewModel.rerunAI(for: appeal)
        XCTAssertFalse(viewModel.canRerun(appeal))
        XCTAssertTrue(viewModel.canRefreshAIRerun(appeal))
        await viewModel.rerunAI(for: appeal)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)

        CannedFeedURLProtocol.errors[path] = URLError(.notConnectedToInternet)
        await viewModel.refreshPendingAIRerun(for: appeal)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
        CannedFeedURLProtocol.errors.removeValue(forKey: path)
        await viewModel.refreshPendingAIRerun(for: appeal)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])

        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            aiPublicResponse: "New draft", aiDraftedAt: "2026-07-01T10:01:00Z"
        )), 200)
        await viewModel.refreshPendingAIRerun(for: appeal)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])

        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            aiPublicResponse: "New draft", aiDraftedAt: "2026-07-01T10:01:00Z",
            lifecycleChangeId: "change-2"
        )), 200)
        await viewModel.refreshPendingAIRerun(for: appeal)
        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertEqual(viewModel.appeals.first?.aiPublicResponse, "New draft")
        XCTAssertEqual(viewModel.drafts[appeal.id], "Local edit")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testNegativeAdmissionUnlocksButLostPostResponseNeedsReadReconciliation() async throws {
        let path = "/api/v1/appeals/appeal-1"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        var declined = try XCTUnwrap(JSONSerialization.jsonObject(with: Support.queued) as? [String: Any])
        declined["queued"] = false
        declined["rerun_by_id"] = NSNull()
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = try (
            JSONSerialization.data(withJSONObject: declined), 200
        )
        await viewModel.rerunAI(for: appeal)
        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertTrue(viewModel.canRerun(appeal))

        CannedFeedURLProtocol.errors["\(path)/resolution-drafts"] = URLError(.networkConnectionLost)
        await viewModel.rerunAI(for: appeal)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertFalse(viewModel.canRerun(appeal))
        CannedFeedURLProtocol.errors.removeValue(forKey: "\(path)/resolution-drafts")
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal()), 200)
        await viewModel.refreshPendingAIRerun(for: appeal)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 2)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
    }

    func testDefiniteClientRejectionReleasesPendingAdmission() async throws {
        let path = "/api/v1/appeals/appeal-1/resolution-drafts"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"rejected"}"#.utf8), 400)

        await viewModel.rerunAI(for: appeal)

        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertTrue(viewModel.canRerun(appeal))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testRequestTimeoutResponseKeepsPendingAdmission() async throws {
        let path = "/api/v1/appeals/appeal-1/resolution-drafts"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"timeout"}"#.utf8), 408)

        await viewModel.rerunAI(for: appeal)
        await viewModel.rerunAI(for: appeal)

        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertFalse(viewModel.canRerun(appeal))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testSuccessfulPostWithUndecodableResponseRetainsAdmission() async throws {
        let path = "/api/v1/appeals/appeal-1/resolution-drafts"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 202)

        await viewModel.rerunAI(for: appeal)
        await viewModel.rerunAI(for: appeal)

        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertFalse(viewModel.canRerun(appeal))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testSameOwnerReopenKeepsAdmissionUntilReadConfirmsResult() async throws {
        let path = "/api/v1/appeals/appeal-1"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal()), 200)
        let store = AppealAIRerunAdmissionStore()
        let first = try sessionViewModel(store: store, userId: "staff-1")
        await first.load()
        let appeal = try XCTUnwrap(first.appeals.first)
        await first.rerunAI(for: appeal)

        let reopened = try sessionViewModel(store: store, userId: "staff-1")
        await reopened.load()
        let reopenedAppeal = try XCTUnwrap(reopened.appeals.first)
        XCTAssertFalse(reopened.canRerun(reopenedAppeal))
        await reopened.rerunAI(for: reopenedAppeal)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)

        let otherOwner = try sessionViewModel(store: store, userId: "staff-2")
        await otherOwner.load()
        let otherAppeal = try XCTUnwrap(otherOwner.appeals.first)
        XCTAssertTrue(otherOwner.canRerun(otherAppeal))
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            aiDraftedAt: "2026-07-01T10:01:00Z", lifecycleChangeId: "change-2"
        )), 200)
        await reopened.refreshPendingAIRerun(for: reopenedAppeal)
        let completedAppeal = try XCTUnwrap(reopened.appeals.first)
        XCTAssertTrue(reopened.canRerun(completedAppeal))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testAdmissionOwnerNormalizesEquivalentApiAndSeparatesDifferentBasePath() throws {
        let standardURL = try XCTUnwrap(URL(string: "https://EXAMPLE.test/api/"))
        let equivalentURL = try XCTUnwrap(URL(string: "https://example.test:443/api"))
        let differentPathURL = try XCTUnwrap(URL(string: "https://example.test/other"))
        let standard = AppealAIRerunOwner(
            userId: "staff-1", baseURL: standardURL
        )
        let equivalent = AppealAIRerunOwner(
            userId: "staff-1", baseURL: equivalentURL
        )
        let differentPath = AppealAIRerunOwner(
            userId: "staff-1", baseURL: differentPathURL
        )
        XCTAssertEqual(standard, equivalent)
        XCTAssertNotEqual(standard, differentPath)
    }

    func testLateOldClaimRemovalCannotClearNewerAdmission() throws {
        let appeal = try decodedAppeal()
        let baseURL = try XCTUnwrap(URL(string: "https://example.test/api"))
        let owner = AppealAIRerunOwner(
            userId: "staff-1", baseURL: baseURL
        )
        let store = AppealAIRerunAdmissionStore()
        let first = try XCTUnwrap(store.admit(appeal, for: owner))
        let advanced = try decodedAppeal(
            aiDraftedAt: "2026-07-01T10:01:00Z", lifecycleChangeId: "change-2"
        )
        store.reconcile([advanced], for: owner)
        let second = try XCTUnwrap(store.admit(appeal, for: owner))

        store.remove(appeal.id, for: owner, matching: first.token)

        XCTAssertEqual(store.admission(appeal.id, for: owner)?.token, second.token)
    }

    func testListReloadKeepsPendingUntilBothResultMarkersAdvance() async throws {
        let path = "/api/v1/appeals/appeal-1"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal()), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        await viewModel.rerunAI(for: appeal)

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal(
            aiDraftedAt: "2026-07-01T10:01:00Z"
        )]), 200)
        await viewModel.load()
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
        let updatedAppeal = try XCTUnwrap(viewModel.appeals.first)
        XCTAssertFalse(viewModel.canRerun(updatedAppeal))

        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal(
            aiDraftedAt: "2026-07-01T10:01:00Z", lifecycleChangeId: "change-2"
        )]), 200)
        await viewModel.load()
        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        let completedAppeal = try XCTUnwrap(viewModel.appeals.first)
        XCTAssertTrue(viewModel.canRerun(completedAppeal))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testStatusSwitchRejectsHeldResultAndKeepsPendingForSameAppeal() async throws {
        let path = "/api/v1/appeals/appeal-1"
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal()), 200)
        await viewModel.rerunAI(for: appeal)

        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            aiDraftedAt: "2026-07-01T10:01:00Z", lifecycleChangeId: "change-2"
        )), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let held = Task { await viewModel.refreshPendingAIRerun(for: appeal) }
        guard await Support.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await held.value
            return
        }
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.list([Support.appeal(id: "other", status: "resolved")]), 200
        )
        await viewModel.selectStatus(.resolved)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await held.value

        XCTAssertEqual(viewModel.appeals.map(\.id), ["other"])
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])
    }

    private func staffViewModel() throws -> ModerationAppealsViewModel {
        try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true,
            isSiteModerator: false, rerunPollAttempts: 1, rerunPollDelay: {}
        )
    }

    private func sessionViewModel(
        store: AppealAIRerunAdmissionStore, userId: String
    ) throws -> ModerationAppealsViewModel {
        try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true,
            isSiteModerator: false, currentUserId: userId, admissionStore: store,
            rerunPollAttempts: 1, rerunPollDelay: {}
        )
    }

    private func decodedAppeal(
        aiDraftedAt: String? = "2026-07-01T10:00:00Z",
        lifecycleChangeId: String? = "change-1"
    ) throws -> ModerationAppeal {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(ModerationAppeal.self, from: Data(Support.appeal(
            aiDraftedAt: aiDraftedAt, lifecycleChangeId: lifecycleChangeId
        ).utf8))
    }
}
