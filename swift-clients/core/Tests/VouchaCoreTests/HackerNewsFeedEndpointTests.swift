import Foundation
@testable import VouchaAPI
import XCTest

final class HackerNewsFeedEndpointTests: XCTestCase {
    func testReferenceFiltersPreserveScopeCursorMediaAndTime() {
        for enabled in [true, false] {
            let endpoints = [
                Endpoint.posts(
                    feedType: "follow_users",
                    after: "opaque==",
                    limit: 9,
                    postTypes: "link",
                    timeRange: "1m",
                    hasHackerNewsReferences: enabled
                ),
                .rssFeedItems(
                    feedType: "audio",
                    after: "opaque==",
                    limit: 9,
                    mediaType: "audio",
                    hasHackerNewsReferences: enabled
                ),
                .allRssFeedItems(after: "opaque==", limit: 9, mediaType: "audio", hasHackerNewsReferences: enabled)
            ]
            XCTAssertEqual(endpoints.map(\.path), [
                "/api/v1/feeds/posts/follow_users",
                "/api/v1/feeds/rss_feed_items/audio",
                "/api/v1/rss-feed-items"
            ])
            for endpoint in endpoints {
                let query = Dictionary(uniqueKeysWithValues: endpoint.queryItems.map { ($0.name, $0.value) })
                XCTAssertEqual(query["has_hacker_news_references"], enabled ? "true" : "false")
                XCTAssertEqual(query["after"], "opaque==")
                XCTAssertEqual(query["limit"], "9")
            }
            XCTAssertEqual(endpoints[0].queryItems.first { $0.name == "time_range" }?.value, "1m")
            XCTAssertEqual(endpoints[0].queryItems.first { $0.name == "post_types" }?.value, "link")
            XCTAssertEqual(endpoints[1].queryItems.first { $0.name == "media_type" }?.value, "audio")
            XCTAssertEqual(endpoints[2].queryItems.first { $0.name == "media_type" }?.value, "audio")
        }
    }

    func testDefaultsOmitReferenceFilter() {
        for endpoint in [Endpoint.posts(), .rssFeedItems(), .allRssFeedItems()] {
            XCTAssertFalse(endpoint.queryItems.contains { $0.name == "has_hacker_news_references" })
        }
    }
}
