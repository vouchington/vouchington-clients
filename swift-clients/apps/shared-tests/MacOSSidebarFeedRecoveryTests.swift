#if os(macOS)
    import Foundation
    import XCTest

    final class MacOSSidebarFeedRecoveryTests: XCTestCase {
        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testVideoFeedRetryRecoversCanonicalRow() throws {
            let fixture = try VideoFeedLaunchFixture.load()
            let app = try launch(fixture, failOnce: true)
            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            LaunchSmokeSidebar.click("Videos", in: app)
            let retry = app.buttons["error-state-retry"]
            XCTAssertTrue(retry.waitForExistence(timeout: 20))
            XCTAssertTrue(retry.isEnabled)
            XCTAssertTrue(retry.isHittable)
            let title = LaunchSmokeSidebar.visibleText(fixture.title, in: app)
            XCTAssertFalse(title.exists)
            retry.click()
            XCTAssertTrue(title.waitForExistence(timeout: 20))
            XCTAssertTrue(retry.waitForNonExistence(timeout: 10))
        }

        private func launch(_ fixture: VideoFeedLaunchFixture, failOnce: Bool = false) throws -> XCUIApplication {
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
            try LaunchSmokeFixtures.configureSession(in: app)
            app.launch()
            app.activate()

            return app
        }
    }
#endif
