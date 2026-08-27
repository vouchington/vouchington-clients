// swiftlint:disable file_length
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeReferralLinksManagementViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testSearchReferralProgramsUsesNativeTopicSearch() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            ApiFixtureLoader.data("web.topics.search.referral-programs.default"),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.searchReferralPrograms(query: "test")

        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first)
        XCTAssertEqual(url.path, "/api/v1/topics")
        XCTAssertTrue(url.absoluteString.contains("topic_types=referral_program"))
        XCTAssertEqual(viewModel.programs.first?.id, "referral-program-1")
    }

    func testCreateReferralLinkPostsAndRefreshesMine() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            ApiFixtureLoader.data("native.referral-links.mine.default"),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        let created = await viewModel.create(
            referralProgramId: "referral-program-1",
            url: "https://example.com/apply/test-card",
            label: "Native"
        )

        XCTAssertTrue(created)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "GET"])
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains("referral-program-1") == true)
        XCTAssertEqual(viewModel.links.first?.id, "referral-link-1")
    }

    func testLoadLinksPreservesPageInfoAndLoadMoreLinksAppends() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["referral-link-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralLinksPage(ids: ["referral-link-2"], endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()
        await viewModel.loadMoreLinks()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links",
            "/api/v1/referral-links"
        ])
        let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = URLComponents(url: lastURL, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(queryItems?.first { $0.name == "after" }?.value, "cursor-1")
        XCTAssertEqual(queryItems?.first { $0.name == "limit" }?.value, "25")
        XCTAssertEqual(viewModel.links.map(\.id), ["referral-link-1", "referral-link-2"])
        XCTAssertFalse(viewModel.hasMoreLinks)
    }

    func testLoadMoreLinksFailurePreservesLoadedContent() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["referral-link-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            emptyReferralClicksFeed(),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.load()
        await viewModel.loadMoreLinks()

        XCTAssertTrue(viewModel.hasLoadedInitialContent)
        XCTAssertEqual(viewModel.links.map(\.id), ["referral-link-1"])
        XCTAssertTrue(viewModel.state.isReferralManagementError)
    }

    func testLoadMoreLinksIgnoresConcurrentTap() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["referral-link-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralLinksPage(ids: ["referral-link-2"], endCursor: nil, hasNextPage: false), 200, 0.1)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()
        async let first: Void = viewModel.loadMoreLinks()
        async let second: Void = viewModel.loadMoreLinks()
        _ = await (first, second)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links",
            "/api/v1/referral-links"
        ])
        XCTAssertEqual(viewModel.links.map(\.id), ["referral-link-1", "referral-link-2"])
    }

    func testLoadMoreClicksIgnoresConcurrentTap() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (referralClicksPage(id: "click-1", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralClicksPage(id: "click-2", endCursor: nil, hasNextPage: false), 200, 0.1)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        async let first: Void = viewModel.loadMoreClicks()
        async let second: Void = viewModel.loadMoreClicks()
        _ = await (first, second)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/referral-clicks",
            "/api/v1/my/referral-clicks"
        ])
        XCTAssertEqual(viewModel.clicks.map(\.id), ["click-1", "click-2"])
    }

    func testRenameUpdatesLabelAndRefreshesLinks() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/referral-links/referral-link-1"] = (Data("{}".utf8), 200)
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.rename(id: "referral-link-1", label: "Renamed")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links/referral-link-1",
            "/api/v1/referral-links"
        ])
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains(#""label":"Renamed""#) == true)
        XCTAssertEqual(viewModel.links.first?.label, "My test card link")
    }

    func testRenameFailurePreservesLoadedContent() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/referral-links/referral-link-1"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            emptyReferralClicksFeed(),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.load()
        await viewModel.rename(id: "referral-link-1", label: "Renamed")

        XCTAssertTrue(viewModel.hasLoadedInitialContent)
        XCTAssertEqual(viewModel.links.first?.id, "referral-link-1")
        XCTAssertTrue(viewModel.state.isReferralManagementError)
    }

    func testSetActivePostsActivationAndRefreshesLinks() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/referral-links/referral-link-1/activations"] = (Data("{}".utf8), 200)
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.setActive(id: "referral-link-1", active: true)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links/referral-link-1/activations",
            "/api/v1/referral-links"
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.first.flatMap { $0 }, "{}")
        XCTAssertEqual(viewModel.links.first?.id, "referral-link-1")
    }

    func testUnsetActiveDeletesActivationAndRefreshesLinks() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/referral-links/referral-link-1/activations"] = (Data("{}".utf8), 200)
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.setActive(id: "referral-link-1", active: false)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links/referral-link-1/activations",
            "/api/v1/referral-links"
        ])
        XCTAssertNil(CannedFeedURLProtocol.capturedBodies.first.flatMap { $0 })
        XCTAssertEqual(viewModel.links.first?.id, "referral-link-1")
    }

    func testDeleteRemovesLinkAndRefreshesLinks() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/referral-links/referral-link-1"] = (Data("{}".utf8), 200)
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.delete(id: "referral-link-1")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/referral-links/referral-link-1",
            "/api/v1/referral-links"
        ])
        XCTAssertNil(CannedFeedURLProtocol.capturedBodies.first.flatMap { $0 })
        XCTAssertEqual(viewModel.links.first?.id, "referral-link-1")
    }

    func testLoadLinksSurfacesGenericErrorFallback() async throws {
        ThrowingFeedURLProtocol.lastURL = nil
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let client = APIClient(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [ThrowingFeedURLProtocol.self]
        )
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()

        XCTAssertEqual(ThrowingFeedURLProtocol.lastURL?.path, "/api/v1/referral-links")
        XCTAssertTrue(viewModel.state.isReferralManagementGenericApiError)
    }

    func testInitialLoadFailureKeepsBlockingState() async throws {
        ThrowingFeedURLProtocol.lastURL = nil
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let client = APIClient(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [ThrowingFeedURLProtocol.self]
        )
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.load()

        XCTAssertFalse(viewModel.hasLoadedInitialContent)
        XCTAssertTrue(viewModel.state.isReferralManagementGenericApiError)
    }

    func testCreateFailureReturnsFalseAndSetsErrorState() async throws {
        ThrowingFeedURLProtocol.lastURL = nil
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let client = APIClient(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [ThrowingFeedURLProtocol.self]
        )
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        let created = await viewModel.create(
            referralProgramId: "referral-program-1",
            url: "https://example.com/apply/test-card",
            label: "Native"
        )

        XCTAssertFalse(created)
        XCTAssertEqual(ThrowingFeedURLProtocol.lastURL?.path, "/api/v1/referral-links")
        XCTAssertTrue(viewModel.state.isReferralManagementGenericApiError)
    }

    func testCreateFailureSurfacesBackendUserErrorText() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            Data(
                """
                {
                  "code": "INVALID_REFERRAL_URL",
                  "user_error_text": "Use a URL from the selected referral program."
                }
                """.utf8
            ),
            422
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        let created = await viewModel.create(
            referralProgramId: "referral-program-1",
            url: "https://example.com/not-allowed",
            label: nil
        )

        XCTAssertFalse(created)
        XCTAssertEqual(
            viewModel.state.referralManagementErrorDescription,
            "Use a URL from the selected referral program."
        )
        XCTAssertNil(LoadState.loaded.referralManagementErrorDescription)
    }

    func testLoadLinksWithoutPageInfoClearsPagination() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            Data(#"{"results":[]}"#.utf8),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()

        XCTAssertFalse(viewModel.hasMoreLinks)
    }

    func testAnalyticsLoadsReferralClicks() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (ApiFixtureLoader.data("native.referral-clicks.mine.default"), 200, 0),
            (ApiFixtureLoader.data("native.referral-clicks.mine.default"), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        await viewModel.loadClicks(after: "referral-click-2")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/referral-clicks")
        let lastClickURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = URLComponents(
            url: lastClickURL,
            resolvingAgainstBaseURL: false
        )?.queryItems
        XCTAssertEqual(queryItems?.first { $0.name == "after" }?.value, "referral-click-2")
        XCTAssertEqual(queryItems?.first { $0.name == "limit" }?.value, "25")
        XCTAssertEqual(viewModel.clicks.map(\.id), [
            "referral-click-1",
            "referral-click-2"
        ])
        XCTAssertEqual(viewModel.clickUsers["user-2"]?.username, "newmember")
    }

    func testLoadMoreClicksUsesStoredCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (referralClicksPage(id: "click-1", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralClicksPage(id: "click-2", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        await viewModel.loadMoreClicks()

        let lastClickURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = URLComponents(
            url: lastClickURL,
            resolvingAgainstBaseURL: false
        )?.queryItems
        XCTAssertEqual(queryItems?.first { $0.name == "after" }?.value, "cursor-1")
        XCTAssertEqual(viewModel.clicks.map(\.id), ["click-1", "click-2"])
        XCTAssertFalse(viewModel.hasMoreClicks)
    }

    func testFetchValidationInfoReturnsHintsAndTreatsNotFoundAsEmpty() async throws {
        let path = "/api/v1/topics/referral-program-1/referral-program/validation-info"
        let response = """
        {"validation_info":{"user_help_text":"Paste an application URL.","example_urls":["https://example.com/apply"]}}
        """
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(response.utf8), 200, 0),
            (Data("{}".utf8), 404, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        let validationInfo = await viewModel.fetchValidationInfo(referralProgramId: "referral-program-1")
        let missingInfo = await viewModel.fetchValidationInfo(referralProgramId: "referral-program-1")

        XCTAssertEqual(validationInfo?.userHelpText, "Paste an application URL.")
        XCTAssertEqual(validationInfo?.exampleUrls.first, "https://example.com/apply")
        XCTAssertNil(missingInfo)
    }

    func testFetchValidationInfoFailureSetsErrorState() async throws {
        ThrowingFeedURLProtocol.lastURL = nil
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let client = APIClient(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [ThrowingFeedURLProtocol.self]
        )
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        let validationInfo = await viewModel.fetchValidationInfo(referralProgramId: "referral-program-1")

        XCTAssertNil(validationInfo)
        XCTAssertEqual(
            ThrowingFeedURLProtocol.lastURL?.path,
            "/api/v1/topics/referral-program-1/referral-program/validation-info"
        )
        XCTAssertTrue(viewModel.state.isReferralManagementGenericApiError)
    }

    private func seedReferralLinksFeed() {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            ApiFixtureLoader.data("native.referral-links.mine.default"),
            200
        )
    }

    private func referralLinksPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)"}"# }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "results": [\(results)],
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(cursor),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func referralClicksPage(id: String, endCursor: String?, hasNextPage: Bool) -> Data {
        Data(
            """
            {
              "results": [{ "id": "\(id)" }],
              "clicks": {
                "\(id)": {
                  "id": "\(id)",
                  "landing_url": "https://example.com/\(id)",
                  "signed_up_at": null,
                  "user_id": null,
                  "created_at": "2026-03-01T12:00:00Z"
                }
              },
              "users": {},
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(endCursor.map { "\"\($0)\"" } ?? "null"),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func emptyReferralClicksFeed() -> Data {
        Data(
            """
            {
              "results": [],
              "clicks": {},
              "users": {},
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }
}

private extension LoadState {
    var isReferralManagementError: Bool {
        if case .error = self {
            return true
        }
        return false
    }

    var isReferralManagementGenericApiError: Bool {
        if case .error(.api(statusCode: 0, preconditionCode: nil)) = self {
            return true
        }
        return false
    }

    var referralManagementErrorDescription: String? {
        guard case let .error(error) = self else { return nil }
        return error.errorDescription
    }
}

final class ThrowingFeedURLProtocol: URLProtocol {
    static var lastURL: URL?

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lastURL = request.url
        client?.urlProtocol(self, didFailWithError: NSError(domain: "VouchaTests", code: 42))
    }

    override func stopLoading() {}
}
