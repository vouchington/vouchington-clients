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
            clickSidebar("Podcasts", in: app)
            let title = visibleText(fixture.title, in: app)
            XCTAssertTrue(title.waitForExistence(timeout: 20))

            clickSidebar("News", in: app)
            XCTAssertTrue(title.waitForNonExistence(timeout: 10))
            clickSidebar("Podcasts", in: app)
            XCTAssertTrue(title.waitForExistence(timeout: 20))
        }

        private func clickSidebar(_ label: String, in app: XCUIApplication) {
            let row = app.outlines.staticTexts.matching(displayedText(label)).firstMatch
            XCTAssertTrue(row.waitForExistence(timeout: 20), "Missing sidebar row \(label)")
            if row.isHittable {
                row.click()
            } else {
                row.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
        }

        private func visibleText(_ value: String, in app: XCUIApplication) -> XCUIElement {
            app.staticTexts.matching(displayedText(value)).firstMatch
        }

        private func displayedText(_ value: String) -> NSPredicate {
            NSPredicate(format: "value == %@ OR label == %@", value, value)
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
                id: "swift.integration.rss-feed-items.audio",
                path: "/api/v1/feeds/rss_feed_items/any",
                query: ["limit": "20", "media_type": "audio"], root: root, fixtures: fixtures
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
