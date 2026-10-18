import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealsReviewFixTests: NativeRouteSurfaceViewModelTestCase {
    func testApprovalResponseClearsTimedOutRerunWithoutAnotherRead() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel(rerunPollAttempts: 1)
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal()), 200
        )
        await viewModel.rerunAI(for: appeal)
        XCTAssertNotNil(viewModel.pendingAIReruns[appeal.id])

        viewModel.setDraft("Approved reply", for: appeal)
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1"] = (
            Support.envelope(Support.appeal(publicResponse: "Approved reply")), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/appeals/appeal-1/approval"] = (
            Support.envelope(Support.appeal(
                publicResponse: "Approved reply", approvedAt: "2026-07-01T11:00:00Z"
            )), 200
        )
        await viewModel.approve(appeal)

        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertFalse(try viewModel.canRerun(XCTUnwrap(viewModel.appeals.first)))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(
            "/api/v1/appeals/appeal-1/resolution-drafts"
        ), 1)
    }

    func testCancelledBeforeRerunAdmissionDoesNotPostOrKeepPending() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let viewModel = try staffViewModel()
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)

        let cancelled = Task {
            withUnsafeCurrentTask { $0?.cancel() }
            await viewModel.rerunAI(for: appeal)
        }
        await cancelled.value

        XCTAssertNil(viewModel.pendingAIReruns[appeal.id])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(
            "/api/v1/appeals/appeal-1/resolution-drafts"
        ), 0)
    }

    func testLateMutationDoesNotClearNewerAdmission() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.list([Support.appeal()]), 200)
        let store = AppealAIRerunAdmissionStore()
        let viewModel = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true,
            isSiteModerator: false, currentUserId: "staff-1", admissionStore: store
        )
        await viewModel.load()
        let appeal = try XCTUnwrap(viewModel.appeals.first)
        let original = try XCTUnwrap(store.admit(appeal, for: viewModel.admissionOwner))
        var newer: AppealAIRerunAdmissionStore.Admission?
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let approved = try decoder.decode(ModerationAppeal.self, from: Data(Support.appeal(
            approvedAt: "2026-07-01T11:00:00Z"
        ).utf8))

        await viewModel.withMutation(appeal.id) {
            store.remove(appeal.id, for: viewModel.admissionOwner, matching: original.token)
            newer = store.admit(appeal, for: viewModel.admissionOwner)
            viewModel.replace(with: approved)
        }

        XCTAssertEqual(store.admission(appeal.id, for: viewModel.admissionOwner), newer)
    }

    private typealias Support = ModerationAppealsTestSupport

    private func staffViewModel(
        rerunPollAttempts: Int = 20
    ) throws -> ModerationAppealsViewModel {
        try ModerationAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: rerunPollAttempts,
            rerunPollDelay: {}
        )
    }
}
