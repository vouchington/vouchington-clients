#if os(macOS)
    import Foundation
    import XCTest

    final class SourceCreationSmokeTests: XCTestCase {
        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testCreatesAndOpensSourceFromEveryVertical() throws {
            for (section, sourcesTitle) in [
                ("News", "All Sources"),
                ("Podcasts", "All Podcasts"),
                ("Videos", "All Channels")
            ] {
                let app = XCUIApplication()
                app.launchArguments = ["--ui-testing-feed-flow", "-AppleLanguages", "(en)", "-AppleLocale", "en_US"]
                app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
                app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
                app.launchEnvironment["VOUCHA_UI_TESTING_SOURCE_CREATION_RESPONSES"] = try responseScript()
                app.launch()
                app.activate()
                XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
                LaunchSmokeSidebar.click(section, in: app)
                let sources = LaunchSmokeSidebar.visibleText(sourcesTitle, in: app)
                XCTAssertTrue(sources.waitForExistence(timeout: 10))
                sources.click()
                let create = app.buttons["Create"].firstMatch
                XCTAssertTrue(create.waitForExistence(timeout: 10))
                create.click()
                let field = app.textFields.firstMatch
                XCTAssertTrue(field.waitForExistence(timeout: 10))
                LaunchSmokeFieldTyping.type("https://example.test/feed.xml", into: field)
                app.sheets.buttons["Create"].click()
                let open = app.buttons["Open source"].firstMatch
                XCTAssertTrue(open.waitForExistence(timeout: 20))
                open.click()
                XCTAssertTrue(app.sheets.firstMatch.waitForNonExistence(timeout: 10))
                XCTAssertTrue(LaunchSmokeSidebar.visibleText("https://example.test/feed.xml", in: app)
                    .waitForExistence(timeout: 20))
                app.terminate()
            }
        }

        private func responseScript() throws -> String {
            let contract = try LaunchSmokeFixtures.manifest()
            func canonical(_ id: String) throws -> String {
                let fixture = try XCTUnwrap(contract.fixtures.first { $0["id"] as? String == id })
                let file = try XCTUnwrap(fixture["bodyFile"] as? String)
                return try Data(contentsOf: contract.root.appendingPathComponent(file)).base64EncodedString()
            }
            func encoded(_ json: String) -> String {
                Data(json.utf8).base64EncodedString()
            }
            let entityId = "70000000-0000-7000-8000-000000003680"
            let empty = encoded(#"{"results":[],"topic_elections":{},"page_info":{"has_next_page":false}}"#)
            let detail = encoded("""
            {"rss_feed":{"id":"\(entityId)","title":"Created smoke source",
            "rss_feed_url":{"url":"https://example.test/feed.xml"},
            "is_enabled":true,"is_discoverable":true,"topic_id":"smoke-source"},"can_view_latest_crawl":false}
            """)
            let list = encoded("""
            {"results":[{"id":"\(entityId)","title":"Created smoke source","feed_type":"article",
            "rss_feed_url":{"url":"https://example.test/feed.xml"},"is_enabled":true,"is_discoverable":true,
            "topic":{"id":"smoke-source","name":"Created smoke source","slug":"smoke-source","topic_type":"rss_feed"}}],
            "topic_elections":{},"page_info":{"has_next_page":false}}
            """)
            let script = try [
                "GET:/api/v1/my/identity": [canonical("swift.my.identity.default")],
                "POST:/api/v1/rss-feeds": [canonical("native.rss-feeds.create.accepted")],
                "GET:/api/v1/my/import/rss-feeds/70000000-0000-7000-8000-000000003681": [
                    canonical("native.rss-feeds.create.status.pending"),
                    canonical("native.rss-feeds.create.status.completed")
                ],
                "GET:/api/v1/rss-feeds/\(entityId)": [detail, detail],
                "GET:/api/v1/rss-feeds": [list, list, list],
                "GET:/api/v1/feeds/rss_feed_items/any": [empty, empty],
                "GET:/api/v1/feature-flags": [encoded(#"{"flags":{},"overrides":null}"#)]
            ]
            let data = try JSONEncoder().encode(script)
            return try XCTUnwrap(String(data: data, encoding: .utf8))
        }
    }
#endif
