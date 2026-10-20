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
            let path = try XCTUnwrap(ProcessInfo.processInfo.environment["VOUCHA_FILAMENTS_CONTRACT_ROOT"])
            _ = try XCTUnwrap(path.hasPrefix("/") && !path.contains("$(") ? path : nil)
            let root = URL(fileURLWithPath: path, isDirectory: true)
                .appendingPathComponent("api-fixtures/v1", isDirectory: true)
            let manifest = try XCTUnwrap(JSONSerialization.jsonObject(
                with: Data(contentsOf: root.appendingPathComponent("manifest.json"))
            ) as? [String: Any])
            let fixtures = try XCTUnwrap(manifest["fixtures"] as? [[String: Any]])
            let identity = try fixtureData(
                id: "swift.my.identity.default",
                path: "/api/v1/my/identity",
                query: [:], root: root, fixtures: fixtures
            )
            let feed = try fixtureData(
                id: "swift.rss-feed-items.feed.default",
                path: "/api/v1/feeds/rss_feed_items/any",
                query: ["limit": "20", "media_type": "video"], root: root, fixtures: fixtures
            )
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: feed) as? [String: Any])
            let results = try XCTUnwrap(body["results"] as? [[String: Any]])
            let itemID = try XCTUnwrap(results.first?["id"] as? String)
            let items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
            let title = try XCTUnwrap(items[itemID]?["title"] as? String)
            _ = try XCTUnwrap(title.isEmpty ? nil : title)
            return Fixtures(feed: feed, identity: identity, title: title)
        }

        private func fixtureData(
            id: String, path: String, query: [String: String], root: URL, fixtures: [[String: Any]]
        ) throws -> Data {
            let fixture = try XCTUnwrap(fixtures.first { $0["id"] as? String == id })
            XCTAssertEqual(fixture["method"] as? String, "GET")
            XCTAssertEqual(fixture["path"] as? String, path)
            XCTAssertEqual(fixture["query"] as? [String: String] ?? [:], query)
            XCTAssertEqual(fixture["auth"] as? String, "fixture-user")
            XCTAssertEqual(fixture["status"] as? Int, 200)
            let bodyFile = try XCTUnwrap(fixture["bodyFile"] as? String)
            _ = try XCTUnwrap(bodyFile.hasPrefix("responses/") && !bodyFile.contains("..") ? bodyFile : nil)
            return try Data(contentsOf: root.appendingPathComponent(bodyFile))
        }
    }
#endif
