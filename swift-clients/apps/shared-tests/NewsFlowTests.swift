#if os(iOS)
    import Foundation
    import XCTest

    final class NewsFlowTests: XCTestCase {
        private struct Fixtures {
            let feed: Data
            let identity: Data
            let primaryID: String
            let primaryTitle: String
            let relatedTitle: String
        }

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testNewsRelatedArticlesExpandAndCollapse() throws {
            let fixture = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = ["--ui-testing-feed-flow", "-AppleLanguages", "(en)", "-AppleLocale", "en_US"]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"] = fixture.feed.base64EncodedString()
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixture.identity.base64EncodedString()
            app.launch()

            // Fixture identity restoration precedes mounting RootView and loading its feed.
            // Keep launch readiness separate from the existing content assertion deadline.
            XCTAssertTrue(
                app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20),
                "News fixture did not finish identity restoration and mount the native root"
            )
            let primary = app.staticTexts[fixture.primaryTitle].firstMatch
            XCTAssertTrue(primary.waitForExistence(timeout: 20))
            let related = app.staticTexts[fixture.relatedTitle].firstMatch
            XCTAssertFalse(related.exists, "Related articles must start collapsed")
            let expand = app.buttons["story-related-\(fixture.primaryID)"]
            XCTAssertTrue(expand.waitForExistence(timeout: 10))
            XCTAssertTrue(expand.isHittable)
            expand.tap()
            XCTAssertTrue(related.waitForExistence(timeout: 10))
            XCTAssertTrue(primary.exists)

            XCTAssertTrue(expand.isHittable)
            expand.tap()
            XCTAssertTrue(related.waitForNonExistence(timeout: 10))
            XCTAssertTrue(primary.exists, "Collapsing related articles must retain the primary news row")
        }

        private func loadFixtures() throws -> Fixtures {
            let contract = try LaunchSmokeFixtures.manifest()
            let identity = try LaunchSmokeFixtures.fixtureData(
                id: "swift.my.identity.default",
                path: "/api/v1/my/identity",
                query: [:], root: contract.root, fixtures: contract.fixtures
            )
            // Replay the producer's article-feed response unchanged. Its request uses a
            // 25-row web page; this native UI requests 20. This is interaction coverage,
            // not a claim that the native request matches that producer fixture's query.
            // Its items currently declare video media despite the article request; the
            // dedicated News transport rejects video requests so this still tests News routing.
            let feed = try LaunchSmokeFixtures.fixtureData(
                id: "web.rss-feed-items.feed.default",
                path: "/api/v1/feeds/rss_feed_items/any",
                query: ["limit": "25", "media_type": "article"],
                root: contract.root, fixtures: contract.fixtures
            )
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
            let results = try XCTUnwrap(body["results"] as? [[String: Any]])
            let result = try XCTUnwrap(results.first)
            let primaryID = try XCTUnwrap(result["entity_id"] as? String)
            let storyID = try XCTUnwrap(result["story_id"] as? String)
            let pages = try XCTUnwrap(body["story_member_pages"] as? [String: [String: Any]])
            let peers = try XCTUnwrap(pages[storyID]?["item_ids"] as? [String])
            let relatedID = try XCTUnwrap(peers.first { $0 != primaryID })
            let items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
            let primaryTitle = try title(items[primaryID])
            let relatedTitle = try title(items[relatedID])
            XCTAssertNotEqual(primaryTitle, relatedTitle)
            return Fixtures(
                feed: feed, identity: identity, primaryID: primaryID,
                primaryTitle: primaryTitle, relatedTitle: relatedTitle
            )
        }

        private func title(_ item: [String: Any]?) throws -> String {
            let data = try XCTUnwrap(item?["data"] as? [String: Any])
            let title = try XCTUnwrap(data["title"] as? String)
            return try XCTUnwrap(title.isEmpty ? nil : title)
        }
    }
#endif
