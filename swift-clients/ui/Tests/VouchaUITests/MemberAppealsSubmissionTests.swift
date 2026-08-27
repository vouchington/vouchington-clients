import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealsSubmissionTests: NativeRouteSurfaceViewModelTestCase {
    func testValidatesReasonAndDetailsBeforeSending() async throws {
        let sut = try makeViewModel()
        sut.beginAppeal(warningTarget)
        await sut.submitAppeal()
        guard case .failed = sut.submissionState else { return XCTFail("Expected validation failure") }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testRejectsBlankAndOversizedDetailsBeforeSending() async throws {
        let sut = try makeViewModel()
        sut.beginAppeal(warningTarget)
        sut.setReason(.other)

        sut.setDetails(" \n ")
        await sut.submitAppeal()
        guard case .failed = sut.submissionState else {
            return XCTFail("Expected blank-details validation failure")
        }

        sut.setDetails(String(repeating: "a", count: 3_801))
        await sut.submitAppeal()
        guard case .failed = sut.submissionState else {
            return XCTFail("Expected oversized-details validation failure")
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testRejectsDetailsWhoseEncodedPayloadExceedsServerUtf16Limit() async throws {
        let sut = try makeViewModel()
        sut.beginAppeal(warningTarget)
        sut.setReason(.other)
        sut.setDetails(String(repeating: "😀", count: 2_000))

        await sut.submitAppeal()

        guard case .failed = sut.submissionState else {
            return XCTFail("Expected UTF-16 payload validation failure")
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testDraftSupportsEveryCanonicalReason() {
        let store = MemberAppealDraftStore()
        for reason in ModerationAppealReason.allCases {
            store.setReason(reason, for: warningTarget, currentUserId: "user-1")
            XCTAssertEqual(store.draft(for: warningTarget, currentUserId: "user-1").reason, reason)
        }
        XCTAssertEqual(ModerationAppealReason.allCases.count, 5)
    }

    func testSuspensionOmitsTargetIdAndRemovalIncludesKind() {
        let suspension = MemberAppealTarget.suspension(date: .now)
        let removal = MemberAppealTarget.removal(
            id: "p1", title: nil, community: nil, kind: .community, date: .now
        )
        XCTAssertNil(suspension.targetId)
        XCTAssertEqual(removal.targetId, "p1")
        XCTAssertEqual(removal.postRemovalKind, .community)
    }

    func testDraftPersistsAcrossViewModelsInSameSessionStore() {
        let store = MemberAppealDraftStore()
        let first = MemberAppealsViewModel(
            client: nil, isSignedIn: true, currentUserId: "user-1", route: .warnings, draftStore: store
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: first)
        first.beginAppeal(warningTarget)
        first.setReason(.wrongRule)
        first.setDetails("The cited rule does not apply.")
        first.cancelAppeal()
        let second = MemberAppealsViewModel(
            client: nil, isSignedIn: true, currentUserId: "user-1", route: .warnings, draftStore: store
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: second)
        second.beginAppeal(warningTarget)
        XCTAssertEqual(second.activeDraft.reason, .wrongRule)
        XCTAssertEqual(second.activeDraft.details, "The cited rule does not apply.")
    }

    func testSuspensionDraftsAreScopedToTheSuspensionInstance() {
        let store = MemberAppealDraftStore()
        let first = MemberAppealTarget.suspension(date: Date(timeIntervalSince1970: 1_700_000_000))
        let second = MemberAppealTarget.suspension(date: Date(timeIntervalSince1970: 1_800_000_000))

        store.setReason(.other, for: first, currentUserId: "user-1")
        store.setDetails("Previous suspension", for: first, currentUserId: "user-1")

        XCTAssertNotEqual(first.id, second.id)
        XCTAssertEqual(store.draft(for: second, currentUserId: "user-1"), MemberAppealDraft())
    }

    func testSuccessfulSubmissionClearsDraftAndAddsTrackingRow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (submissionResponse(isDuplicate: true), 200)
        let sut = try makeViewModel()
        prepareValidSubmission(sut)
        await sut.submitAppeal()
        XCTAssertEqual(sut.submissionState, .succeeded(isDuplicate: true))
        XCTAssertEqual(sut.appeals.map(\.id), ["appeal-1"])
        XCTAssertEqual(sut.activeDraft, MemberAppealDraft())
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 1)
    }

    func testConcurrentSubmissionSendsExactlyOnce() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (submissionResponse(), 200)
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/appeals")
        let sut = try makeViewModel()
        prepareValidSubmission(sut)

        let first = Task { await sut.submitAppeal() }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/appeals"
        )
        XCTAssertTrue(didSuspend)
        await sut.submitAppeal()
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 1)
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/appeals")
        await first.value
        XCTAssertEqual(sut.submissionState, .succeeded(isDuplicate: false))
    }

    func testRecoverableFailureRetainsDraftClearsTokenAndRetries() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (Data(#"{"message":"Unavailable"}"#.utf8), 503, 0),
            (submissionResponse(), 200, 0)
        ]
        let sut = try makeViewModel()
        prepareValidSubmission(sut)

        await sut.submitAppeal()
        guard case .failed = sut.submissionState else { return XCTFail("Expected request failure") }
        XCTAssertEqual(sut.activeDraft.reason, .incorrectFacts)
        XCTAssertEqual(sut.activeDraft.details, "The notice identifies the wrong event.")
        XCTAssertNil(sut.turnstileToken)

        sut.turnstileToken = "retry-token"
        await sut.submitAppeal()
        XCTAssertEqual(sut.submissionState, .succeeded(isDuplicate: false))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 2)
    }

    func testCancelledSubmissionRetainsDraftAndReturnsToIdle() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (submissionResponse(), 200)
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/appeals")
        let sut = try makeViewModel()
        prepareValidSubmission(sut)

        let submission = Task { await sut.submitAppeal() }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/appeals"
        )
        XCTAssertTrue(didSuspend)
        submission.cancel()
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/appeals")
        await submission.value

        XCTAssertEqual(sut.submissionState, .idle)
        XCTAssertEqual(sut.activeDraft.reason, .incorrectFacts)
        XCTAssertEqual(sut.activeDraft.details, "The notice identifies the wrong event.")
        XCTAssertNil(sut.turnstileToken)
    }

    func testCancelledNetworkErrorReturnsToIdle() async throws {
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.cancelled)
        let sut = try makeViewModel()
        prepareValidSubmission(sut)

        await sut.submitAppeal()

        XCTAssertEqual(sut.submissionState, .idle)
        XCTAssertNil(sut.turnstileToken)
        XCTAssertEqual(sut.activeDraft.reason, .incorrectFacts)
    }

    func testPendingAppealForTargetPreventsSubmissionWhenAnotherRecordIsResolved() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (submissionResponse(), 200)
        let sut = try makeViewModel()
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let existing = try decoder.decode(
            ModerationAppealListResponse.self,
            from: ModerationAppealsTestSupport.list([
                ModerationAppealsTestSupport.appeal(id: "appeal-1", status: "resolved"),
                ModerationAppealsTestSupport.appeal(id: "appeal-2")
            ])
        )
        sut.pendingAppealPagination.reset(items: existing.appeals)
        prepareValidSubmission(sut)

        await sut.submitAppeal()

        XCTAssertEqual(sut.pendingAppealPagination.items.map(\.id), ["appeal-1", "appeal-2"])
        XCTAssertEqual(sut.pendingAppealPagination.items.first?.status, .resolved)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals"), 0)
    }

    func testDraftAccessWithoutUserAndNonVouchaErrorMessageAreSafe() {
        let sut = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: nil,
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        sut.activeTarget = warningTarget
        sut.setReason(.other)
        sut.setDetails("Ignored")

        XCTAssertEqual(sut.activeDraft, MemberAppealDraft())
        XCTAssertEqual(sut.message(for: TestError.example), .verbatim("Example failure"))
    }

    func testSessionDraftsAreIsolatedAcrossAccountsIncludingSuspensions() {
        let store = MemberAppealDraftStore()
        let target = MemberAppealTarget.suspension(date: .now)
        let first = MemberAppealsViewModel(
            client: nil, isSignedIn: true, currentUserId: "user-1", route: .suspension, draftStore: store
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: first)
        first.beginAppeal(target)
        first.setReason(.other)
        first.setDetails("User one private draft")

        let second = MemberAppealsViewModel(
            client: nil, isSignedIn: true, currentUserId: "user-2", route: .suspension, draftStore: store
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: second)
        second.beginAppeal(target)
        XCTAssertEqual(second.activeDraft, MemberAppealDraft())
    }

    private var warningTarget: MemberAppealTarget {
        .warning(id: "warning-1", message: nil, community: nil, createdAt: .now)
    }

    private func makeViewModel() throws -> MemberAppealsViewModel {
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: viewModel)
        return viewModel
    }

    private func prepareValidSubmission(_ viewModel: MemberAppealsViewModel) {
        viewModel.beginAppeal(warningTarget)
        viewModel.setReason(.incorrectFacts)
        viewModel.setDetails("The notice identifies the wrong event.")
        viewModel.turnstileToken = "token"
    }

    private func submissionResponse(isDuplicate: Bool = false) -> Data {
        Data("""
        {"appeal":\(ModerationAppealsTestSupport.appeal()),"is_duplicate":\(isDuplicate)}
        """.utf8)
    }
}

private enum TestError: LocalizedError {
    case example

    var errorDescription: String? {
        "Example failure"
    }
}
