import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationListOutcomeRecoveryTests: NativeRouteSurfaceViewModelTestCase {
    func testAppealResolutionDuringListLoadFetchesFreshPage() async throws {
        let path = "/api/v1/appeals"
        let model = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        let original = ModerationAppealsTestSupport.appeal(sentAt: "2026-07-01T12:00:00Z")
        let appeal = try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationAppealEnvelope.self,
            from: ModerationAppealsTestSupport.envelope(original)
        )
        .appeal
        CannedFeedURLProtocol.handlers[path] = (ModerationAppealsTestSupport.staffList([original]), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let load = Task { await model.load() }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await load.value
            return
        }
        CannedFeedURLProtocol.handlers[path + "/appeal-1/resolution"] = (
            ModerationAppealsTestSupport.envelope(ModerationAppealsTestSupport.appeal(
                status: "resolved",
                sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "reduce"
            )), 200
        )
        await model.resolve(appeal, action: .reduce)
        CannedFeedURLProtocol.handlers[path] = (ModerationAppealsTestSupport.staffList([]), 200)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await load.value
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        XCTAssertTrue(model.summaryPagination.items.isEmpty)
        XCTAssertTrue(model.summaryPagination.hasLoadedPage)
        XCTAssertFalse(model.summaryPagination.isLoading)
        XCTAssertFalse(model.isLoading)
        XCTAssertNil(model.errorMessage)
    }

    func testRepeatedDisputeOutcomeInvalidationStopsAfterOneRetryWithRecoverableError() async throws {
        let path = "/api/v1/disputes"
        let model = try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        let detail = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: ReviewDisputesTestSupport.envelope(ReviewDisputesTestSupport.dispute())
        ).dispute
        CannedFeedURLProtocol.handlers[path] = (ReviewDisputesTestSupport.staffList([]), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let load = Task { await model.load() }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await load.value
            return
        }
        model.replaceWithTargetedOutcome(detail)
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await load.value
            return
        }
        model.replaceWithTargetedOutcome(detail)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await load.value
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        XCTAssertFalse(model.isLoading)
        XCTAssertFalse(model.summaryPagination.isLoading)
        XCTAssertNotNil(model.errorMessage)
        XCTAssertNotNil(model.summaryPagination.lastError)
        await model.load()
        XCTAssertNil(model.errorMessage)
        XCTAssertTrue(model.summaryPagination.hasLoadedPage)
    }
}
