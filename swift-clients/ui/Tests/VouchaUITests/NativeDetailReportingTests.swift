import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeDetailReportingTests: NativeRouteSurfaceViewModelTestCase {
    func testMissingUsernamesDoNotMakeAnotherProfileTheViewer() async throws {
        let profile = Data(#"{"user":{"id":"user-2","username":null},"profile_links":[]}"#.utf8)
        let identityCases: [(Data, Int)] = [
            (Data("{}".utf8), 500),
            (PrivateUserTestFixture.identityEnvelope(
                id: "viewer", overrides: ["username": NSNull()]
            ), 200)
        ]
        for (identity, status) in identityCases {
            CannedFeedURLProtocol.handlers = [
                "/api/v1/users/user-2": (profile, 200),
                "/api/v1/my/identity": (identity, status)
            ]
            let viewModel = try await load(path: "/user/user-2", destination: .userProfile)

            XCTAssertFalse(viewModel.detailRelationIsSelfProfile)
            XCTAssertEqual(viewModel.detailReportTarget, .user(id: "user-2"))
        }
    }

    func testDomainAndUrlRoutesUseCanonicalHostnameReportTarget() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(), 200)
        let domain = try await load(path: "/domain/example.com", destination: .domainDetail)
        XCTAssertEqual(domain.detailReportTarget, .urlHostname(id: "hostname-1"))

        for path in ["/url/url-1", "/url/url-1/crawls", "/url/url-1/crawls/crawl-1"] {
            CannedFeedURLProtocol.handlers = Self.urlHandlers
            CannedFeedURLProtocol.capturedURLs = []
            let url = try await load(path: path, destination: .urlDetail)
            XCTAssertEqual(url.detailReportTarget, .urlHostname(id: "hostname-1"), path)
        }
    }

    func testBlockedMissingAndSignedOutHostnamesCannotBeReported() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(blocked: true), 200)
        let blocked = try await load(path: "/domain/example.com", destination: .domainDetail)
        XCTAssertNil(blocked.detailReportTarget)
        XCTAssertFalse(blocked.rows.contains { $0.title == "Report" })

        CannedFeedURLProtocol.handlers = ["/api/v1/urls/url-1": (Self.urlData(includeHostname: false), 200)]
        let missing = try await load(path: "/url/url-1", destination: .urlDetail)
        XCTAssertNil(missing.detailReportTarget)

        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(), 200)
        let signedOut = try await load(path: "/domain/example.com", destination: .domainDetail)
        let surface = try NativeDetailSurface(
            entry: entry(for: .domainDetail),
            viewModel: signedOut,
            isSignedIn: false
        )
        XCTAssertThrowsError(try surface.inspect().find(button: "Report"))
    }

    func testReportSubmissionTrimsNotePreventsDuplicatesAndTracksSuccess() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .domainDetail), client: makeClient())
        viewModel.detailReportTarget = .urlHostname(id: "hostname-1")

        let first = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "spam",
            note: "context",
            turnstileToken: "token"
        )
        let duplicate = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "other",
            note: nil,
            turnstileToken: "token-2"
        )

        XCTAssertTrue(first)
        XCTAssertFalse(duplicate)
        XCTAssertEqual(viewModel.detailReportSubmissionState, .submitted)
        XCTAssertTrue(viewModel.detailReportSuccessPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/reports" }.count, 1)

        let surface = try NativeDetailSurface(entry: entry(for: .domainDetail), viewModel: viewModel)
        XCTAssertEqual(surface.reportSuccessTitle, "Report submitted")
        XCTAssertEqual(surface.reportSuccessMessage, "Thank you. Moderators will review it.")
        surface.reportSuccessBinding.wrappedValue = false
        XCTAssertFalse(viewModel.detailReportSuccessPresented)
        XCTAssertEqual(viewModel.detailReportSubmissionState, .submitted)
    }

    func testNetworkFailureIsRetryableAndCancellationSendsNoRequest() async throws {
        CannedFeedURLProtocol.errors["/api/v1/reports"] = URLError(.notConnectedToInternet)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .domainDetail), client: makeClient())
        viewModel.detailReportTarget = .urlHostname(id: "hostname-1")
        viewModel.detailPendingReportReason = "spam"
        viewModel.detailPendingReportNote = "context"

        let failed = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "spam",
            note: "context",
            turnstileToken: "spent-token"
        )
        XCTAssertFalse(failed)
        XCTAssertEqual(viewModel.detailReportSubmissionState, .idle)
        XCTAssertEqual(viewModel.detailPendingReportReason, "spam")

        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let retried = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "spam",
            note: "context",
            turnstileToken: "fresh-token"
        )
        XCTAssertTrue(retried)

        let requestCount = CannedFeedURLProtocol.capturedURLs.count
        let surface = try NativeDetailSurface(entry: entry(for: .domainDetail), viewModel: viewModel)
        viewModel.detailPendingReportReason = "other"
        viewModel.detailPendingReportNote = "draft"
        surface.cancelReportDetail()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requestCount)
        XCTAssertNil(viewModel.detailPendingReportReason)
        XCTAssertEqual(viewModel.detailPendingReportNote, "")
    }

    func testReportFailurePreservesDraftForRetryWithFreshTurnstile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data(#"{"message":"invalid"}"#.utf8), 422)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .domainDetail), client: makeClient())
        viewModel.detailReportTarget = .urlHostname(id: "hostname-1")
        viewModel.detailPendingReportReason = "misinformation"
        viewModel.detailPendingReportNote = "context"

        let failed = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "misinformation",
            note: "context",
            turnstileToken: "spent-token"
        )

        XCTAssertFalse(failed)
        XCTAssertEqual(viewModel.detailReportSubmissionState, .idle)
        XCTAssertEqual(viewModel.detailPendingReportReason, "misinformation")
        XCTAssertEqual(viewModel.detailPendingReportNote, "context")

        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let retried = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "misinformation",
            note: "context",
            turnstileToken: "fresh-token"
        )
        XCTAssertTrue(retried)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""cf_turnstile_response":"fresh-token""#)
        })
    }

    func testReloadResetsReportStateAndExistingUserReportingStillWorks() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let viewModel = try await load(path: "/domain/example.com", destination: .domainDetail)
        _ = await viewModel.report(
            target: .urlHostname(id: "hostname-1"),
            reason: "spam",
            note: nil,
            turnstileToken: "token"
        )
        viewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        await viewModel.load()
        XCTAssertEqual(viewModel.detailReportSubmissionState, .idle)

        viewModel.detailReportTarget = .user(id: "user-1")
        let submitted = await viewModel.report(
            target: .user(id: "user-1"),
            reason: "harassment",
            note: nil,
            turnstileToken: "user-token"
        )
        XCTAssertTrue(submitted)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""entityType":"user""#)
        })
    }

    func testInFlightReportCompletionDoesNotOverwriteReloadedDetailState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/reports"] = [(Data("{}".utf8), 200, 0.15)]
        let viewModel = try await load(path: "/domain/example.com", destination: .domainDetail)

        let report = Task {
            await viewModel.report(
                target: .urlHostname(id: "hostname-1"),
                reason: "spam",
                note: nil,
                turnstileToken: "token"
            )
        }
        for _ in 0 ..< 20 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == "/api/v1/reports" }) {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }

        viewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        await viewModel.load()
        let staleResult = await report.value

        XCTAssertFalse(staleResult)
        XCTAssertEqual(viewModel.detailReportSubmissionState, .idle)
        XCTAssertFalse(viewModel.detailReportSuccessPresented)
        XCTAssertEqual(viewModel.detailReportTarget, .urlHostname(id: "hostname-1"))
    }

    func testUserTagsSheetConstructsTagManagementSurfaceWithFallbackNavigation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.com"] = (Self.domainData(), 200)
        let viewModel = try await load(path: "/domain/example.com", destination: .domainDetail)
        let surface = try NativeDetailSurface(entry: entry(for: .domainDetail), viewModel: viewModel)
        XCTAssertNoThrow(try surface.userTagsSheet.inspect())
    }

    private func load(
        path: String,
        destination: NativeRouteDestinationIdentifier
    ) async throws -> NativeRouteSurfaceViewModel {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path)?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: destination),
            client: makeClient(),
            routeMatch: match
        )
        await viewModel.load()
        return viewModel
    }

    private static func domainData(blocked: Bool = false) -> Data {
        Data("""
        {"hostname":{"id":"hostname-1","hostname":"example.com","is_blocked":\(blocked)},
        "hostname_election":null,"election_vote":null,"rss_feeds":[],"top_urls":[],"topic":null}
        """.utf8)
    }

    private static func urlData(includeHostname: Bool = true) -> Data {
        let hostname = includeHostname
            ? #", "hostname":{"id":"hostname-1","hostname":"example.com","is_blocked":false}"#
            : ""
        return Data("""
        {"can_trigger_crawl":false,"can_view_crawl_history":true,"can_view_latest_crawl":false,
        "latest_crawl":null,"url":{"id":"url-1","url":"https://example.com/a","pathname":"/a"\(hostname)},
        "url_type":"web"}
        """.utf8)
    }

    private static let urlHandlers: [String: (Data, Int)] = [
        "/api/v1/urls/url-1": (urlData(), 200),
        "/api/v1/urls/url-1/crawls": (
            Data(
                """
                {"results":[{"id":"crawl-1","created_at":"2026-01-01T00:00:00Z","response_status_code":200}],\
                "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
                """.utf8
            ),
            200
        ),
        "/api/v1/urls/url-1/crawls/crawl-1": (
            Data(
                #"{"crawl":{"id":"crawl-1","created_at":"2026-01-01T00:00:00Z","response_status_code":200},"og_image_sideload":null}"#
                    .utf8
            ),
            200
        )
    ]
}
