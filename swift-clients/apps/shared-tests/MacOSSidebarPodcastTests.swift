#if os(macOS)
    import Foundation
    import XCTest

    final class MacOSSidebarPodcastTests: XCTestCase {
        private struct Fixtures {
            let feed: Data
            let identity: Data
            let title: String
        }

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testPodcastFeedSurvivesSidebarNavigation() throws {
            let fixture = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = [
                "--ui-testing-feed-flow",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_PODCAST_FEED_RESPONSE_BASE64"] = fixture.feed
                .base64EncodedString()
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixture.identity
                .base64EncodedString()
            app.launch()
            app.activate()

            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            LaunchSmokeSidebar.click("Podcasts", in: app)
            let title = LaunchSmokeSidebar.visibleText(fixture.title, in: app)
            XCTAssertTrue(title.waitForExistence(timeout: 20))

            LaunchSmokeSidebar.click("News", in: app)
            XCTAssertTrue(title.waitForNonExistence(timeout: 10))
            LaunchSmokeSidebar.click("Podcasts", in: app)
            XCTAssertTrue(title.waitForExistence(timeout: 20))
        }

        private func loadFixtures() throws -> Fixtures {
            let contract = try LaunchSmokeFixtures.manifest()
            let identity = try LaunchSmokeFixtures.fixtureData(
                id: "swift.my.identity.default",
                path: "/api/v1/my/identity",
                query: [:], root: contract.root, fixtures: contract.fixtures
            )
            let feed = try LaunchSmokeFixtures.fixtureData(
                id: "swift.integration.rss-feed-items.audio",
                path: "/api/v1/feeds/rss_feed_items/any",
                query: ["limit": "20", "media_type": "audio"], root: contract.root, fixtures: contract.fixtures
            )
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
            let results = try XCTUnwrap(body["results"] as? [[String: Any]])
            let itemID = try XCTUnwrap(results.first?["id"] as? String)
            let items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
            let item = try XCTUnwrap(items[itemID])
            let data = item["data"] as? [String: Any]
            let title = (item["title"] as? String) ?? (data?["title"] as? String)
            let resolved = try XCTUnwrap(title?.isEmpty == false ? title : nil)
            let mediaType = (item["media_type"] as? String) ?? (data?["media_type"] as? String)
            XCTAssertEqual(mediaType, "audio")
            return Fixtures(feed: feed, identity: identity, title: resolved)
        }
    }
#endif
