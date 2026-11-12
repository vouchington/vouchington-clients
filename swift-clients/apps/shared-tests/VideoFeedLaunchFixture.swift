import Foundation
import XCTest

struct VideoFeedLaunchFixture {
    let feed: Data
    let identity: Data
    let title: String

    enum ValidationError: Error {
        case invalidVideoMedia
    }

    static func load() throws -> Self {
        let contract = try LaunchSmokeFixtures.manifest()
        let identity = try LaunchSmokeFixtures.fixtureData(
            id: "swift.my.identity.default",
            path: "/api/v1/my/identity",
            query: [:], root: contract.root, fixtures: contract.fixtures
        )
        let feed = try LaunchSmokeFixtures.fixtureData(
            id: "swift.rss-feed-items.feed.default",
            path: "/api/v1/feeds/rss_feed_items/any",
            query: ["limit": "20", "media_type": "video"], root: contract.root, fixtures: contract.fixtures
        )
        return Self(feed: feed, identity: identity, title: try videoTitle(in: feed))
    }

    static func videoTitle(in feed: Data) throws -> String {
        let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
        let results = try XCTUnwrap(body["results"] as? [[String: Any]])
        let itemID = try XCTUnwrap(results.first?["id"] as? String)
        let items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
        let item = try XCTUnwrap(items[itemID])
        let title = try XCTUnwrap(item["title"] as? String)
        _ = try XCTUnwrap(title.isEmpty ? nil : title)
        let media = try XCTUnwrap(item["media_content"] as? [String: Any])
        guard media["medium"] as? String == "video",
              let contentType = media["type"] as? String,
              contentType.hasPrefix("video/"), contentType.count > "video/".count
        else {
            throw ValidationError.invalidVideoMedia
        }
        return title
    }
}
