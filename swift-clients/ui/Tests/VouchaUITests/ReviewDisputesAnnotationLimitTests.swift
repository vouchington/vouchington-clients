import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesAnnotationLimitTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport
    private let disputesPath = "/api/v1/disputes"
    private let resolutionPath = "/api/v1/disputes/dispute-1/resolution"

    func testAnnotationEditorCapsAtUTF16LimitWithoutSplittingEmoji() async throws {
        let (viewModel, dispute) = try await loadedViewModel()
        let exactLimit = String(repeating: "😀", count: 1_000)

        viewModel.setAnnotationDraft(exactLimit + "a", for: dispute)

        let cappedDraft = viewModel.annotationDraft(for: dispute)
        XCTAssertEqual(cappedDraft, exactLimit)
        XCTAssertEqual(cappedDraft.utf16.count, 2_000)
        XCTAssertTrue(cappedDraft.hasSuffix("😀"))
        XCTAssertTrue(viewModel.canResolve(dispute, action: .annotate))

        CannedFeedURLProtocol.handlers[resolutionPath] = (
            Support.envelope(Support.dispute(
                status: "resolved",
                sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "annotate"
            )), 200
        )
        await viewModel.resolve(dispute, action: .annotate)

        let requestBody = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.last)
        let requestJSON = try XCTUnwrap(
            JSONSerialization.jsonObject(with: Data(requestBody.utf8)) as? [String: String]
        )
        XCTAssertEqual(requestJSON["body_text"], exactLimit)
    }

    func testOverlongAnnotationCannotEnableOrSendResolution() async throws {
        let (viewModel, dispute) = try await loadedViewModel()
        viewModel.annotationDrafts[dispute.id] = String(repeating: "😀", count: 1_000) + "a"

        XCTAssertEqual(viewModel.annotationDraft(for: dispute).utf16.count, 2_001)
        XCTAssertFalse(viewModel.canResolve(dispute, action: .annotate))

        await viewModel.resolve(dispute, action: .annotate)

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(resolutionPath), 0)
    }

    private func loadedViewModel() async throws -> (ReviewDisputesViewModel, ReviewDispute) {
        CannedFeedURLProtocol.handlers[disputesPath] = (
            Support.list([Support.dispute(sentAt: "2026-07-01T12:00:00Z")]), 200
        )
        let viewModel = try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollDelay: {}
        )
        await viewModel.load()
        return try (viewModel, XCTUnwrap(viewModel.disputes.first))
    }
}
