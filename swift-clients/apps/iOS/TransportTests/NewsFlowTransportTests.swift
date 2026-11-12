import Foundation
import XCTest

final class NewsFlowTransportTests: XCTestCase {
    private let newsKey = "VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"
    private let identityKey = "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"

    func testNewsModeAcceptsOnlyNativeArticleQueryAndIdentity() throws {
        let news = Data("news-response".utf8)
        let identity = Data("identity-response".utf8)
        let environment = [
            newsKey: news.base64EncodedString(), identityKey: identity.base64EncodedString(),
            "VOUCHA_UI_TESTING_SOURCE_CREATION_RESPONSES": "{}"
        ]
        XCTAssertEqual(try response("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article", environment), news)
        XCTAssertEqual(try response("/api/v1/my/identity", environment), identity)
        for query in [
            "limit=25&media_type=article", "limit=20", "limit=20&media_type=video",
            "limit=20&media_type=audio", "limit=20&media_type=article&after=cursor",
            "limit=20&media_type=article&media_type=video"
        ] {
            XCTAssertNil(try response("/api/v1/feeds/rss_feed_items/any?\(query)", environment), query)
        }
    }

    func testNewsModeCannotFallBackToOtherConfiguredFeeds() throws {
        let encoded = Data("response".utf8).base64EncodedString()
        let environment = [
            newsKey: encoded,
            "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64": encoded,
            "VOUCHA_UI_TESTING_PODCAST_FEED_RESPONSE_BASE64": encoded,
            "VOUCHA_UI_TESTING_POSTS_FEED_RESPONSE_BASE64": encoded
        ]
        for path in [
            "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video",
            "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=audio",
            "/api/v1/feeds/posts/any?limit=20&sort=hot&time_range=1w",
            "/api/v1/rss-feed-items?limit=20&media_type=article"
        ] {
            XCTAssertNil(try response(path, environment), path)
        }
    }

    func testArticleReplayRequiresNewsModeAndRejectsOtherHostsAndMethods() throws {
        XCTAssertNil(try response("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article", [:]))
        let environment = [newsKey: Data("response".utf8).base64EncodedString()]
        var request = try URLRequest(url: XCTUnwrap(URL(
            string: "https://example.com/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article"
        )))
        request.httpMethod = "GET"
        XCTAssertNil(FeedFlowUITestingURLProtocol.canonicalResponseData(for: request, environment: environment))
        request.url = try XCTUnwrap(URL(
            string: "https://ui-testing.voucha.invalid/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article"
        ))
        request.httpMethod = "POST"
        XCTAssertNil(FeedFlowUITestingURLProtocol.canonicalResponseData(for: request, environment: environment))
    }

    private func response(_ path: String, _ environment: [String: String]) throws -> Data? {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://ui-testing.voucha.invalid\(path)")))
        request.httpMethod = "GET"
        return FeedFlowUITestingURLProtocol.canonicalResponseData(for: request, environment: environment)
    }
}
