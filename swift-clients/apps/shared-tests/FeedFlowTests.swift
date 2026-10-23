#if os(iOS)
    import Foundation
    import XCTest

    final class FeedFlowTests: XCTestCase {
        private struct Fixtures {
            let feed: Data
            let identity: Data
            let title: String
        }

        func testVideoFeedLoadsCanonicalRowAndSurvivesTabNavigation() throws {
            let fixture = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = ["--ui-testing-feed-flow", "-AppleLanguages", "(en)", "-AppleLocale", "en_US"]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64"] = fixture.feed.base64EncodedString()
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixture.identity.base64EncodedString()
            app.launch()

            XCTAssertTrue(app.otherElements["root-shell"].waitForExistence(timeout: 20))
            let videos = app.tabBars.buttons["Videos"]
            XCTAssertTrue(videos.waitForExistence(timeout: 10))
            videos.tap()
            XCTAssertTrue(app.staticTexts[fixture.title].firstMatch.waitForExistence(timeout: 20))

            app.tabBars.buttons["News"].tap()
            videos.tap()
            XCTAssertTrue(app.staticTexts[fixture.title].firstMatch.waitForExistence(timeout: 10))
        }

        private func loadFixtures() throws -> Fixtures {
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
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
            let results = try XCTUnwrap(body["results"] as? [[String: Any]])
            let itemID = try XCTUnwrap(results.first?["id"] as? String)
            let items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
            let title = try XCTUnwrap(items[itemID]?["title"] as? String)
            _ = try XCTUnwrap(title.isEmpty ? nil : title)
            return Fixtures(feed: feed, identity: identity, title: title)
        }
    }
#endif
