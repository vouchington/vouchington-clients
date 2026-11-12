import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SourceCreationServiceTests: NativeRouteSurfaceViewModelTestCase {
    func testLiveCanonicalCreateAndStatusRequests() async throws {
        let client = try makeClient()
        let service = SourceCreationService.live(client: client)
        let id = "70000000-0000-7000-8000-000000003681"
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            ApiFixtureLoader.data("native.rss-feeds.create.accepted"), 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/import/rss-feeds/" + id] = (
            ApiFixtureLoader.data("native.rss-feeds.create.status.completed"), 200
        )
        let accepted = try await service.create("https://example.com/feed")
        XCTAssertEqual(accepted.import.id, id)
        let status = try await service.status(id)
        XCTAssertEqual(status.import.pendingRows, 0)
        XCTAssertEqual(status.rows.count, 1)
        let requests = CannedFeedURLProtocol.capturedRequests
        XCTAssertEqual(requests.map(\.method), ["POST", "GET"])
        let body = try XCTUnwrap(requests[0].body).data(using: .utf8)!
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
        XCTAssertEqual(object["rss_feed_url"] as? String, "https://example.com/feed")
        XCTAssertEqual(object["follow"] as? Bool, true)
    }

    func testLiveTopicResolutionChecksFeedIdentity() async throws {
        let service = try SourceCreationService.live(client: makeClient())
        let data = Data(
            #"{"rss_feed":{"id":"feed-1","rss_feed_url":{"url":"https://example.com/feed"},"is_enabled":true,"is_discoverable":true,"topic_id":"topic-1"},"can_view_latest_crawl":false}"#
                .utf8
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/feed-1"] = (data, 200)
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/other"] = (data, 200)
        let topic = try await service.topic("feed-1")
        let mismatch = try await service.topic("other")
        XCTAssertEqual(topic, "topic-1")
        XCTAssertNil(mismatch)
    }
}
