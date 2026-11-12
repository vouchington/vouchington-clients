import Foundation
import XCTest

final class FeedFlowUITestingURLProtocolTests: XCTestCase {
    private let feedKey = "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64"
    private let body = Data("canonical response bytes".utf8)

    func testVideoResponseAndOneShotFailureAreSharedAcrossRequests() throws {
        let fixture = try VideoFeedLaunchFixture.load()
        XCTAssertFalse(fixture.title.isEmpty)
        try assertInvalidVideoMediaRejected(in: fixture.feed)
        let scenario = makeScenario(body: fixture.feed)
        let first = load("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video", scenario: scenario)
        XCTAssertEqual((first.error as? URLError)?.code, .notConnectedToInternet)
        XCTAssertNil(first.response)
        XCTAssertTrue(first.data.isEmpty)
        XCTAssertFalse(first.finished)

        let retry = load("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video", scenario: scenario)
        XCTAssertNil(retry.error)
        XCTAssertEqual(retry.response?.statusCode, 200)
        XCTAssertEqual(retry.data, fixture.feed)
        XCTAssertTrue(retry.finished)
    }

    func testUnrelatedAndRejectedRequestsDoNotConsumeVideoFailure() {
        let scenario = makeScenario()
        let identity = load("/api/v1/my/identity", scenario: scenario)
        XCTAssertTrue(identity.finished)
        for path in [
            "/api/v1/feeds/rss_feed_items/any?limit=25&media_type=video",
            "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article",
            "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video&cursor=next"
        ] {
            let rejected = load(path, scenario: scenario)
            XCTAssertEqual((rejected.error as? URLError)?.code, .unsupportedURL)
            XCTAssertFalse(rejected.finished)
        }
        let video = load("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video", scenario: scenario)
        XCTAssertEqual((video.error as? URLError)?.code, .notConnectedToInternet)
    }

    func testConcurrentVideoRequestsConsumeExactlyOneFailure() {
        let scenario = makeScenario()
        let results = Results()
        DispatchQueue.concurrentPerform(iterations: 20) { _ in
            let client = self.load(
                "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video", scenario: scenario
            )
            results.record(client)
        }
        XCTAssertEqual(results.failures, 1)
        XCTAssertEqual(results.successes, 19)
    }

    func testStoppedRequestDeliversNothingAndPreservesFailureForNextRequest() {
        let scenario = makeScenario()
        let client = Client()
        let transport = transport(scenario: scenario, client: client)
        transport.stopLoading()
        transport.startLoading()
        XCTAssertNil(client.error)
        XCTAssertNil(client.response)
        XCTAssertTrue(client.data.isEmpty)
        XCTAssertFalse(client.finished)
        let next = load("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video", scenario: scenario)
        XCTAssertEqual((next.error as? URLError)?.code, .notConnectedToInternet)
    }

    func testStopDuringResponseCallbackPreventsBodyAndFinish() {
        let scenario = makeScenario(failOnce: false)
        let client = Client()
        client.stopOnResponse = true
        transport(scenario: scenario, client: client).startLoading()
        XCTAssertEqual(client.response?.statusCode, 200)
        XCTAssertNil(client.error)
        XCTAssertTrue(client.data.isEmpty)
        XCTAssertFalse(client.finished)
    }

    private func makeScenario(failOnce: Bool = true, body: Data? = nil) -> FeedFlowUITestingURLProtocol.Scenario {
        .init(environment: [
            feedKey: (body ?? self.body).base64EncodedString(),
            "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64": Data("identity".utf8).base64EncodedString(),
            "VOUCHA_UI_TESTING_VIDEO_FAIL_ONCE": failOnce ? "1" : "0"
        ])
    }

    private func load(_ path: String, scenario: FeedFlowUITestingURLProtocol.Scenario) -> Client {
        let client = Client()
        let request = URLRequest(url: requestURL(path))
        let transport = FeedFlowUITestingURLProtocol(request: request, cachedResponse: nil, client: client)
        transport.scenario = scenario
        transport.startLoading()
        return client
    }

    private func transport(
        scenario: FeedFlowUITestingURLProtocol.Scenario, client: Client
    ) -> FeedFlowUITestingURLProtocol {
        let request = URLRequest(url: requestURL(
            "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video"
        ))
        let transport = FeedFlowUITestingURLProtocol(request: request, cachedResponse: nil, client: client)
        transport.scenario = scenario
        return transport
    }

    private func requestURL(_ path: String) -> URL {
        guard let url = URL(string: "https://ui-testing.voucha.invalid" + path) else {
            preconditionFailure("Invalid test request path: \(path)")
        }
        return url
    }

    private final class Results {
        private let lock = NSLock()
        private(set) var failures = 0
        private(set) var successes = 0
        func record(_ client: Client) {
            lock.lock()
            defer { lock.unlock() }
            if (client.error as? URLError)?.code == .notConnectedToInternet { failures += 1 }
            if client.finished { successes += 1 }
        }
    }

    private final class Client: NSObject, URLProtocolClient {
        var error: Error?
        var response: HTTPURLResponse?
        var data = Data()
        var finished = false
        var stopOnResponse = false
        func urlProtocol(_: URLProtocol, wasRedirectedTo _: URLRequest, redirectResponse _: URLResponse) {}
        func urlProtocol(_: URLProtocol, cachedResponseIsValid _: CachedURLResponse) {}
        func urlProtocol(
            _ transport: URLProtocol,
            didReceive response: URLResponse,
            cacheStoragePolicy _: URLCache.StoragePolicy
        ) {
            self.response = response as? HTTPURLResponse
            if stopOnResponse { transport.stopLoading() }
        }

        func urlProtocol(_: URLProtocol, didLoad data: Data) {
            self.data.append(data)
        }

        func urlProtocolDidFinishLoading(_: URLProtocol) {
            finished = true
        }

        func urlProtocol(_: URLProtocol, didFailWithError error: Error) {
            self.error = error
        }

        func urlProtocol(_: URLProtocol, didReceive _: URLAuthenticationChallenge) {}
        func urlProtocol(_: URLProtocol, didCancel _: URLAuthenticationChallenge) {}
    }
}

private extension FeedFlowUITestingURLProtocolTests {
    func assertInvalidVideoMediaRejected(in feed: Data) throws {
        let canonical = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
        let results = try XCTUnwrap(canonical["results"] as? [[String: Any]])
        let itemID = try XCTUnwrap(results.first?["id"] as? String)
        let items = try XCTUnwrap(canonical["rss_feed_items"] as? [String: [String: Any]])
        let item = try XCTUnwrap(items[itemID])
        let media = try XCTUnwrap(item["media_content"] as? [String: Any])
        for (field, invalidValue) in [
            ("medium", "audio"), ("medium", nil), ("type", "audio/mpeg"),
            ("type", nil), ("type", "video/")
        ] as [(String, String?)] {
            var invalidMedia = media
            invalidMedia[field] = invalidValue
            var invalidItem = item
            invalidItem["media_content"] = invalidMedia
            var invalidItems = items
            invalidItems[itemID] = invalidItem
            var invalidFeed = canonical
            invalidFeed["rss_feed_items"] = invalidItems
            let invalidData = try JSONSerialization.data(withJSONObject: invalidFeed)
            XCTAssertThrowsError(try VideoFeedLaunchFixture.videoTitle(in: invalidData)) { error in
                XCTAssertTrue(error is VideoFeedLaunchFixture.ValidationError)
            }
        }
    }
}
