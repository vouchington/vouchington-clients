#if os(macOS)
    import Foundation
    import XCTest

    final class MacOSSidebarPostsTests: XCTestCase {
        private struct Fixtures {
            let feed: Data
            let identity: Data
            let title: String
        }

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testPostsFeedSurvivesSidebarNavigation() throws {
            let fixture = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = [
                "--ui-testing-feed-flow",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_POSTS_FEED_RESPONSE_BASE64"] = fixture.feed
                .base64EncodedString()
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixture.identity
                .base64EncodedString()
            app.launch()
            app.activate()

            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            LaunchSmokeSidebar.click("Posts", in: app)
            let title = LaunchSmokeSidebar.visibleText(fixture.title, in: app)
            XCTAssertTrue(title.waitForExistence(timeout: 20))

            LaunchSmokeSidebar.click("News", in: app)
            XCTAssertTrue(title.waitForNonExistence(timeout: 10))
            LaunchSmokeSidebar.click("Posts", in: app)
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
                id: "swift.posts.feed.default",
                path: "/api/v1/feeds/posts/any",
                query: ["limit": "20", "sort": "hot"], root: contract.root, fixtures: contract.fixtures
            )
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
            let results = try XCTUnwrap(body["results"] as? [[String: Any]])
            let postID = try XCTUnwrap(results.first?["entity_id"] as? String)
            let posts = try XCTUnwrap(body["posts"] as? [String: [String: Any]])
            let title = try XCTUnwrap(posts[postID]?["title"] as? String)
            _ = try XCTUnwrap(title.isEmpty ? nil : title)
            return Fixtures(feed: feed, identity: identity, title: title)
        }
    }
#endif
