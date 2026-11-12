#if os(macOS)
    import Foundation
    import XCTest

    final class MacOSSidebarFeedRecoveryTests: XCTestCase {
        private struct Fixtures {
            let feed: Data
            let identity: Data
            let title: String
        }

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testVideoFeedRetryRecoversCanonicalRow() throws {
            let fixture = try loadFixtures()
            let app = launch(fixture, failOnce: true)
            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            LaunchSmokeSidebar.click("Videos", in: app)
            let retry = app.buttons["Retry"]
            XCTAssertTrue(retry.waitForExistence(timeout: 20))
            XCTAssertTrue(retry.isEnabled)
            XCTAssertTrue(retry.isHittable)
            let title = LaunchSmokeSidebar.visibleText(fixture.title, in: app)
            XCTAssertFalse(title.exists)
            retry.click()
            XCTAssertTrue(title.waitForExistence(timeout: 20))
            XCTAssertTrue(retry.waitForNonExistence(timeout: 10))
        }

        private func launch(_ fixture: Fixtures, failOnce: Bool = false) -> XCUIApplication {
            let app = XCUIApplication()
            app.launchArguments = [
                "--ui-testing-feed-flow",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64"] = fixture.feed
                .base64EncodedString()
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixture.identity
                .base64EncodedString()
            if failOnce {
                app.launchEnvironment["VOUCHA_UI_TESTING_VIDEO_FAIL_ONCE"] = "1"
            }
            app.launch()
            app.activate()

            return app
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
            let item = try XCTUnwrap(items[itemID])
            let data = item["data"] as? [String: Any]
            let title = (item["title"] as? String) ?? (data?["title"] as? String)
            let resolved = try XCTUnwrap(title?.isEmpty == false ? title : nil)
            let mediaType = (item["media_type"] as? String) ?? (data?["media_type"] as? String)
            XCTAssertEqual(mediaType, "video")
            return Fixtures(feed: feed, identity: identity, title: resolved)
        }
    }
#endif
