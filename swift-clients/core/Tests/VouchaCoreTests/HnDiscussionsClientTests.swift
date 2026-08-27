import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaCore
import XCTest

final class HnDiscussionURLCollectorTests: XCTestCase {
    func testNormalizeLowercasesHostAndStripsHashSlashAndUtm() {
        XCTAssertEqual(
            HnDiscussionURLCollector.normalize(
                "https://News.YCombinator.com/item/?utm_source=x&id=1#comments"
            ),
            "https://news.ycombinator.com/item?id=1"
        )
    }

    func testNormalizeRejectsNonHTTPURLs() {
        XCTAssertNil(HnDiscussionURLCollector.normalize("not a url"))
        XCTAssertNil(HnDiscussionURLCollector.normalize("javascript:alert(1)"))
    }

    func testCollectDedupesAndCapsAtThree() {
        XCTAssertEqual(
            HnDiscussionURLCollector.collect([
                "https://example.com/a/",
                "https://EXAMPLE.com/a",
                "https://example.com/b?utm_campaign=1",
                "https://example.com/c",
                "https://example.com/d"
            ]),
            [
                "https://example.com/a/",
                "https://example.com/b?utm_campaign=1",
                "https://example.com/c"
            ]
        )
    }

    func testExtractPullsHTTPURLsFromMarkdown() {
        XCTAssertEqual(
            HnDiscussionURLCollector.extract(fromMarkdown: "See https://example.com/a and https://example.com/b."),
            ["https://example.com/a", "https://example.com/b"]
        )
    }
}

final class HnDiscussionsClientTests: XCTestCase {
    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
        super.tearDown()
    }

    func testMapHitsKeepsMatchingTitleScoreAndComments() throws {
        let data = Data("""
        {"hits":[
          {"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18},
          {"objectID":"999","title":"Other","url":"https://other.example/a","points":1,"num_comments":0},
          {"objectID":"124","title":"","url":"https://example.com/a","points":2,"num_comments":2}
        ]}
        """.utf8)
        let itemURL = try XCTUnwrap(URL(string: "https://news.ycombinator.com/item?id=123"))
        XCTAssertEqual(
            HnDiscussionsClient.mapHits(data, sourceURL: "https://example.com/a"),
            [
                HnDiscussionThread(
                    objectID: "123",
                    title: "Example",
                    score: 42,
                    commentCount: 18,
                    itemURL: itemURL
                )
            ]
        )
    }

    func testSearchURLRestrictsToStoryURLs() throws {
        let url = try XCTUnwrap(HnDiscussionsClient.searchURL(for: "https://example.com/a"))
        let components = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false))
        XCTAssertEqual(components.host, "hn.algolia.com")
        XCTAssertEqual(components.path, "/api/v1/search")
        XCTAssertEqual(components.queryItems?.first { $0.name == "query" }?.value, "https://example.com/a")
        XCTAssertEqual(
            components.queryItems?.first { $0.name == "restrictSearchableAttributes" }?.value,
            "url"
        )
        XCTAssertEqual(components.queryItems?.first { $0.name == "tags" }?.value, "story")
        XCTAssertEqual(components.queryItems?.first { $0.name == "hitsPerPage" }?.value, "5")
    }

    func testMapHitsReturnsEmptyForInvalidJSON() {
        XCTAssertEqual(
            HnDiscussionsClient.mapHits(Data("not-json".utf8), sourceURL: "https://example.com/a"),
            []
        )
    }

    func testSearchFetchesMatchingThreadsAndSkipsFailedResponses() async throws {
        CapturingURLProtocol.responseData = Data("""
        {"hits":[{"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18}]}
        """.utf8)
        let client = HnDiscussionsClient(session: makeSearchSession())
        let threads = await client.search(urls: ["https://example.com/a", "not a url"])
        XCTAssertEqual(threads.map(\.objectID), ["123"])
        let requestURL = try XCTUnwrap(CapturingURLProtocol.lastRequestURL)
        XCTAssertEqual(requestURL.host, "hn.algolia.com")

        CapturingURLProtocol.responseStatusCode = 503
        let failed = await client.search(url: "https://example.com/a")
        XCTAssertEqual(failed, [])
    }

    private func makeSearchSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CapturingURLProtocol.self]
        return URLSession(configuration: configuration)
    }
}
