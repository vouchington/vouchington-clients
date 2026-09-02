import VouchaAPI
import VouchaCore
import XCTest
#if !canImport(Darwin)
    import FoundationNetworking
#endif

// MARK: - Response envelope for the RSS feed-item feed endpoint

/// The `/api/v1/feeds/rss_feed_items/:feed_type` response is NOT a plain
/// `Page<RssFeedItem>` — `results` contains ID-reference rows and the
/// hydrated items live in the `rss_feed_items` sidecar dict.
///
/// `Item` mirrors the `view_rss_feed_items` SQL view which exposes
/// `media_type` as a top-level column, NOT nested under `media_content`.
/// The shared `RssFeedItem` model uses a different shape designed for a
/// different serialisation path; integration tests assert against this
/// local DTO to avoid coupling to the shared model's in-progress shape.
private struct RssFeedItemFeedPage: Decodable {
    struct ResultItem: Decodable {
        let entityId: String
    }

    /// Subset of `view_rss_feed_items` columns needed for media-type assertions.
    struct Item: Decodable {
        let id: String
        let mediaType: String?
    }

    let results: [ResultItem]
    let rssFeedItems: [String: Item]
}

/// Real integration tests that hit a live Voucha API server.
/// These tests are skipped unless VOUCHA_RUN_INTEGRATION=1 is set.
///
/// Local setup:
///   ./dev/initialize web && source .env
///   node backend/scripts/tests/seed-swift-integration.mts
///   export VOUCHA_RUN_INTEGRATION=1
///   swift test --package-path swift-clients/core --filter VouchaIntegrationTests
final class VouchaIntegrationTests: XCTestCase {
    private static var apiClient: APIClient?

    override static func setUp() {
        super.setUp()
        // Inject session cookies if provided by the seed script
        let env = ProcessInfo.processInfo.environment
        guard env["VOUCHA_RUN_INTEGRATION"] == "1" else {
            apiClient = nil
            return
        }

        let config = AppConfig.from(environment: env)
        let cookieStorage = HTTPCookieStorage.shared
        if let deviceToken = env["VOUCHA_TEST_DT"],
           let sessionToken = env["VOUCHA_TEST_ST"],
           let url = URL(string: config.baseURL.absoluteString) {
            func makeCookie(_ name: String, _ value: String) -> HTTPCookie? {
                HTTPCookie(properties: [
                    .name: name,
                    .value: value,
                    .domain: config.baseURL.host ?? "localhost",
                    .path: "/"
                ])
            }
            let cookies = [
                makeCookie("dt", deviceToken),
                makeCookie("st", sessionToken)
            ].compactMap { $0 }
            cookieStorage.setCookies(cookies, for: url, mainDocumentURL: URL?.none)
        }
        apiClient = APIClient(config: config, cookieStorage: cookieStorage)
    }

    private func requireIntegration() throws {
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["VOUCHA_RUN_INTEGRATION"] == "1",
            "Set VOUCHA_RUN_INTEGRATION=1 to run integration tests"
        )
    }

    func testSharedVideoFixtureMatchesFeedEnvelope() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let page = try decoder.decode(
            RssFeedItemFeedPage.self,
            from: ApiFixtureLoader.data("swift.integration.rss-feed-items.video")
        )
        let items = page.results.compactMap { page.rssFeedItems[$0.entityId] }
        XCTAssertEqual(items.map(\.mediaType), ["video"])
    }

    func testRssFeedItemsAudioMediaTypeFilter() async throws {
        try requireIntegration()
        let client = try XCTUnwrap(Self.apiClient)
        let endpoint = Endpoint.rssFeedItems(feedType: "any", limit: 5, mediaType: "audio")
        let page: RssFeedItemFeedPage = try await client.send(endpoint)
        let items = page.results.compactMap { page.rssFeedItems[$0.entityId] }
        XCTAssertFalse(items.isEmpty, "Expected to find at least one audio item to verify the filter.")
        // All returned items must be audio — mediaType is the top-level column in view_rss_feed_items
        for item in items {
            XCTAssertEqual(
                item.mediaType, "audio",
                "Expected audio item, got mediaType=\(item.mediaType ?? "nil") for id=\(item.id)"
            )
        }
    }

    func testRssFeedItemsVideoMediaTypeFilter() async throws {
        try requireIntegration()
        let client = try XCTUnwrap(Self.apiClient)
        let endpoint = Endpoint.rssFeedItems(feedType: "any", limit: 5, mediaType: "video")
        let page: RssFeedItemFeedPage = try await client.send(endpoint)
        let items = page.results.compactMap { page.rssFeedItems[$0.entityId] }
        XCTAssertFalse(items.isEmpty, "Expected to find at least one video item to verify the filter.")
        for item in items {
            XCTAssertEqual(
                item.mediaType, "video",
                "Expected video item, got mediaType=\(item.mediaType ?? "nil") for id=\(item.id)"
            )
        }
    }
}
