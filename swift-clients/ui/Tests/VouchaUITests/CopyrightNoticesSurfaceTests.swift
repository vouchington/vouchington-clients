import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
@testable import VouchaModels
import VouchaTestSupport
import XCTest

@MainActor
final class CopyrightNoticesSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testAcceptedNoticeListAndDetailRoutesRequireAuthentication() throws {
        let list = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/copyright/notices"))
        let detail = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/copyright/notices/case-42"))

        XCTAssertEqual(list.entry.destinationIdentifier, .copyrightNotices)
        XCTAssertEqual(detail.entry.destinationIdentifier, .copyrightNotices)
        XCTAssertEqual(detail.match.param("id"), "case-42")
        XCTAssertTrue(NativeRouteDestinationIdentifier.copyrightNotices.requiresAuthenticatedSession)

        let signedOut = NativeRouteDestinationView(
            entry: detail.entry,
            routeMatch: detail.match,
            isSignedIn: false
        )
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(text: "Copyright notice"))
    }

    func testListLoadsAndNavigatesToCurrentPublicClaimantProfile() async throws {
        let path = "/api/v1/copyright-notices"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"{"copyright_notices":[{"id":"case-42","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":{"user_id":"user-42","display_name":"Current claimant"}}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        var navigatedPath: String?
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: nil) { navigatedPath = $0 }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            let inspected = try hostedSurface.inspect()
            XCTAssertNoThrow(try inspected.find(text: "Accepted copyright notices"))
            XCTAssertNoThrow(try inspected.find(text: "Current claimant"))
            try inspected.find(button: "Current claimant").tap()
            XCTAssertEqual(navigatedPath, "/user/user-42")
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [path])
        }
    }

    func testNullClaimantDoesNotRenderAnIdentityOrProfileLink() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/copyright-notices"] = (
            Data(
                #"{"copyright_notices":[{"id":"case-43","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: nil) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            let inspected = try hostedSurface.inspect()
            XCTAssertThrowsError(try inspected.find(button: "Current claimant"))
            XCTAssertThrowsError(try inspected.find(text: "Claimant:"))
        }
    }

    func testInitialListFailureRendersRetryAndReloadsSuccessfully() async throws {
        let path = "/api/v1/copyright-notices"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"error":"temporary failure"}"#.utf8), 500, 0),
            (
                Data(
                    #"{"copyright_notices":[{"id":"case-retry","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: nil) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        let retryRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")

        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            let failed = try hostedSurface.inspect()
            XCTAssertNoThrow(try failed.find(button: "Try Again"))
            XCTAssertTrue(surface.viewModel.notices.isEmpty)

            CannedFeedURLProtocol.suspendResponse(path: path)
            defer { CannedFeedURLProtocol.releaseResponse(path: path) }
            try failed.find(button: "Try Again").tap()
            _ = try await retryRequest.wait()
            CannedFeedURLProtocol.releaseResponse(path: path)
            await surface.viewModel.loadInitial()

            XCTAssertEqual(surface.viewModel.notices.map(\.id), ["case-retry"])
            XCTAssertNil(surface.viewModel.errorMessage)
            XCTAssertThrowsError(try hostedSurface.inspect().find(button: "Retry"))
        }
    }

    func testLeavingDuringInitialLoadCancelsStaleResponseAndAllowsReload() async throws {
        let path = "/api/v1/copyright-notices"
        let staleBody = Data(
            #"{"copyright_notices":[{"id":"stale-case","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
        let currentBody = Data(
            #"{"copyright_notices":[{"id":"current-case","jurisdiction":"us_dmca","received_at":"2026-07-02T12:00:00Z","accepted_at":"2026-07-02T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
        CannedFeedURLProtocol.queuedHandlers[path] = [(staleBody, 200, 0), (currentBody, 200, 0)]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: nil) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        let viewModel = surface.viewModel
        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        ViewHosting.host(view: hostedSurface)
        _ = try await staleRequest.wait()

        ViewHosting.expel()
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertTrue(viewModel.notices.isEmpty)

        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        ViewHosting.host(view: hostedSurface)
        _ = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await viewModel.loadInitial()

        XCTAssertEqual(viewModel.notices.map(\.id), ["current-case"])
        CannedFeedURLProtocol.releaseResponse(path: path)
        XCTAssertEqual(viewModel.notices.map(\.id), ["current-case"])
    }

    func testDetailUsesParticipantTimelineAndDisplaysPublicCaseFieldsWithoutLegalActions() async throws {
        let id = "case-44"
        let participantPath = "/api/v1/copyright-notices/\(id)/participant"
        let detailPath = "/api/v1/copyright-notices/\(id)"
        let claimant = #""claimant":{"user_id":"user-44","display_name":"Current claimant"}"#
        let target = #""targets":[{"id":"target-44","surface":"post-image","hosted_use_url":"https://voucha.ai/posts/fixture","restriction_status":"active"}]"#
        let detailJSON = """
        {"copyright_notice":{"id":"case-44","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,\(
            claimant
        ),\(target),"timeline":[]}}
        """
        let participantJSON = """
        {"copyright_notice":{"id":"case-44","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,\(
            claimant
        ),\(
            target
        ),"timeline":[{"id":"event-44","event_type":"notice_received","created_at":"2026-07-01T12:00:00Z"}],"viewer_role":"claimant","respondable_target_ids":[],"submissions":[],"statements":[{"id":"statement-44","delivery_kind":"decision","state":"sent","sent_at":"2026-07-01T12:02:00Z","text":"Public reason text"}]}}
        """
        CannedFeedURLProtocol.handlers[participantPath] = (Data(participantJSON.utf8), 200)
        CannedFeedURLProtocol.handlers[detailPath] = (Data(detailJSON.utf8), 200)
        var navigatedPath: String?
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: id) { navigatedPath = $0 }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            let inspected = try hostedSurface.inspect()
            XCTAssertNoThrow(try inspected.find(text: "Copyright notice"))
            XCTAssertNoThrow(try inspected.find(text: "Current claimant"))
            XCTAssertNoThrow(try inspected.find(text: "Affected hosted material"))
            XCTAssertNoThrow(try inspected.find(text: "https://voucha.ai/posts/fixture"))
            XCTAssertNoThrow(try inspected.find(text: "Case timeline"))
            let renderedTexts = try inspected.findAll(ViewType.Text.self).map { try $0.string() }
            XCTAssertTrue(renderedTexts.contains { $0.hasPrefix("Notice received.") }, "Texts: \(renderedTexts)")
            XCTAssertNoThrow(try inspected.find(text: "Public reason text"))
            XCTAssertThrowsError(try inspected.find(text: "Current claimant:"))
            XCTAssertThrowsError(try inspected.find(button: "Appeal"))
            XCTAssertThrowsError(try inspected.find(button: "Counter-notice"))
            try inspected.find(button: "Accepted copyright notices").tap()
            XCTAssertEqual(navigatedPath, "/copyright/notices")
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [participantPath, detailPath])
        }
    }

    func testForbiddenParticipantEndpointFallsBackToPublicNoticeDetail() async throws {
        let id = "00000000-0000-7000-8000-000000000804"
        let participantPath = "/api/v1/copyright-notices/\(id)/participant"
        let detailPath = "/api/v1/copyright-notices/\(id)"
        CannedFeedURLProtocol.handlers[participantPath] = (Data(#"{"error":"forbidden"}"#.utf8), 403)
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("web.copyright.notice.detail.populated"), 200
        )
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: id) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)

        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()

            XCTAssertNil(surface.viewModel.participantNotice)
            XCTAssertEqual(surface.viewModel.notice?.id, id)
            XCTAssertNoThrow(try hostedSurface.inspect().find(text: "Current claimant"))
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [participantPath, detailPath])
        }
    }

    func testLoadMoreUsesOpaqueCursorAndAppendsOnlyNewCases() async throws {
        let path = "/api/v1/copyright-notices"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"{"copyright_notices":[{"id":"case-1","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null}],"page_info":{"has_next_page":true,"start_cursor":null,"end_cursor":"opaque/cursor"}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"{"copyright_notices":[{"id":"case-1","jurisdiction":"us_dmca","received_at":"2026-07-01T12:00:00Z","accepted_at":"2026-07-01T12:01:00Z","provisional_withholding_at":null,"target_count":1,"claimant":null},{"id":"case-2","jurisdiction":"us_dmca","received_at":"2026-07-02T12:00:00Z","accepted_at":"2026-07-02T12:01:00Z","provisional_withholding_at":null,"target_count":2,"claimant":null}],"page_info":{"has_next_page":false,"start_cursor":"opaque/cursor","end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: nil) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            await surface.viewModel.loadMore()
            XCTAssertEqual(surface.notices.map(\.id), ["case-1", "case-2"])
            let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
            let components = try XCTUnwrap(URLComponents(url: lastURL, resolvingAgainstBaseURL: false))
            let after = try XCTUnwrap(components.queryItems?.first { $0.name == "after" })
            XCTAssertEqual(after.value, "opaque/cursor")
        }
    }

    func testEuSettlementsLoadMoreUsesOpaqueCursorAndAppendsOnlyNewRows() async throws {
        let id = "00000000-0000-7000-8000-000000001218"
        let participantPath = "/api/v1/copyright-notices/\(id)/participant"
        let settlementsPath = "/api/v1/copyright-notices/\(id)/eu-dispute-settlements"
        let participant = #"{"copyright_notice":{"id":"00000000-0000-7000-8000-000000001218","jurisdiction":"eu_dsa","received_at":"2026-07-01T12:00:00Z","accepted_at":null,"provisional_withholding_at":null,"target_count":0,"claimant":{"user_id":"private-user","display_name":"Private claimant"},"targets":[],"timeline":[{"id":"private-event","event_type":"notice_received","created_at":"2026-07-01T12:00:00Z"}],"viewer_role":"claimant","respondable_target_ids":[],"submissions":[],"statements":[],"eu":{"outcome":"no_action","decided_at":"2026-07-01T12:10:00Z","informed_at":"2026-07-01T12:11:00Z","reopened_at":null,"complaint":{"can_submit":false,"window_ends_at":null,"request":null,"decision":null},"dispute_settlements":[{"id":"settlement-1","body_name":"First body","referred_at":"2026-07-02T09:00:00Z","outcome":null}],"dispute_settlements_page_info":{"has_next_page":true,"start_cursor":"start","end_cursor":"opaque/eu-cursor"}}}}"#
        CannedFeedURLProtocol.handlers[participantPath] = (Data(participant.utf8), 200)
        CannedFeedURLProtocol.queuedHandlers[settlementsPath] = [
            (Data(#"{"error":"temporary failure"}"#.utf8), 500, 0),
            (
                Data(
                    #"{"copyright_eu_dispute_settlements":[{"id":"settlement-1","body_name":"First body","referred_at":"2026-07-02T09:00:00Z","outcome":null},{"id":"settlement-2","body_name":"Second body","referred_at":"2026-07-03T09:00:00Z","outcome":{"result":"agreement","decided_at":"2026-07-04T09:00:00Z","implemented_at":null}}],"page_info":{"has_next_page":false,"start_cursor":"opaque/eu-cursor","end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let surface = try NativeCopyrightNoticesSurface(client: makeClient(), noticeId: id) { _ in }
        let hostedSurface = surface.environment(\.locale, uiEnglishTestLocale)
        try await ViewHosting.host(hostedSurface) {
            await surface.viewModel.loadInitial()
            XCTAssertNoThrow(try hostedSurface.inspect().find(text: "First body"))
            let privacyTexts = try hostedSurface.inspect().findAll(ViewType.Text.self).map { try $0.string() }
            XCTAssertFalse(privacyTexts.contains { $0.contains("Private claimant") })
            XCTAssertFalse(privacyTexts.contains { $0.contains("Notice received.") })
            XCTAssertThrowsError(try hostedSurface.inspect().find(text: "Case timeline"))
            await surface.viewModel.loadMoreSettlements()
            XCTAssertTrue(surface.viewModel.paginationFailed)
            XCTAssertNoThrow(try hostedSurface.inspect().find(button: "Try Again"))

            CannedFeedURLProtocol.suspendResponse(path: settlementsPath)
            defer { CannedFeedURLProtocol.releaseResponse(path: settlementsPath) }
            let retryRequest = CannedFeedURLProtocol.requestBarrier(path: settlementsPath, method: "GET")
            let settlementsUpdated = expectation(description: "settlement retry updates rendered rows")
            withObservationTracking {
                _ = surface.viewModel.euSettlements.count
            } onChange: {
                settlementsUpdated.fulfill()
            }
            try hostedSurface.inspect().find(button: "Try Again").tap()
            _ = try await retryRequest.wait()
            CannedFeedURLProtocol.releaseResponse(path: settlementsPath)
            await fulfillment(of: [settlementsUpdated], timeout: 1)
            XCTAssertEqual(surface.euSettlements.map(\.id), ["settlement-1", "settlement-2"])
            let inspected = try hostedSurface.inspect()
            XCTAssertNoThrow(try inspected.find(text: "Second body"))
            let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
            let components = try XCTUnwrap(URLComponents(url: lastURL, resolvingAgainstBaseURL: false))
            XCTAssertEqual(components.path, settlementsPath)
            XCTAssertEqual(components.queryItems?.first { $0.name == "after" }?.value, "opaque/eu-cursor")
        }
    }

}
