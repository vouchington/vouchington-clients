import XCTest

final class LaunchSmokeTests: XCTestCase {
    private static let isolatedUITestingArgument = "--ui-testing-launch-smoke"
    private static let isolatedAPIBaseURL = "https://ui-testing.voucha.invalid"
    private static let disabledAppAttestEnvironmentKey = "VOUCHA_DISABLE_APP_ATTEST"
    private static let rootShellAccessibilityIdentifier = "root-shell"
    private static let notificationSettingsHeadingID = "notification-settings-heading"
    private static let notificationSettingsReentryID = "notification-settings-reenter-route"
    private static let notificationSettingsActivationIdentifier =
        "notification-settings-route-activation-count"
    private static let notificationSettingsAnnouncementID =
        "notification-settings-route-announcement"
    private static let fediverseProviders = [
        (identifier: "fediverse-provider-filter-all", label: "All providers"),
        (identifier: "fediverse-provider-filter-peertube", label: "PeerTube"),
        (identifier: "fediverse-provider-filter-mastodon", label: "Mastodon"),
        (identifier: "fediverse-provider-filter-lemmy", label: "Lemmy"),
        (identifier: "fediverse-provider-filter-bluesky", label: "Bluesky")
    ]

    func testLaunchRendersRootShell() {
        let app = makeApplication()
        app.launch()

        XCTAssertTrue(
            app.otherElements[Self.rootShellAccessibilityIdentifier].waitForExistence(timeout: 20)
        )
    }

    #if os(macOS)
        func testNotificationSettingsRouteFocusesHeadingAndAnnouncesLocalizedOpenMessageOnEachActivation() {
            let app = makeApplication()
            app.launchArguments = [
                "--ui-testing-notification-settings",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launch()

            XCTAssertTrue(
                app.otherElements[Self.rootShellAccessibilityIdentifier].waitForExistence(timeout: 20)
            )
            let heading = app.descendants(matching: .any)[Self.notificationSettingsHeadingID]
            XCTAssertTrue(heading.waitForExistence(timeout: 20))
            let activationCount = app.staticTexts[Self.notificationSettingsActivationIdentifier]
            XCTAssertTrue(activationCount.waitForExistence(timeout: 20))
            let announcement = app.staticTexts[Self.notificationSettingsAnnouncementID]
            XCTAssertTrue(announcement.waitForExistence(timeout: 20))
            waitForLabel("1", on: activationCount)
            waitForLabel("Notification settings opened", on: announcement)

            let reentry = app.buttons[Self.notificationSettingsReentryID]
            XCTAssertTrue(reentry.waitForExistence(timeout: 20))
            waitForLabel("1", on: activationCount)
            reentry.click()

            let reactivatedHeading = app.descendants(matching: .any)[
                Self.notificationSettingsHeadingID
            ]
            XCTAssertTrue(reactivatedHeading.waitForExistence(timeout: 20))
            waitForLabel("2", on: activationCount)
            waitForLabel("Notification settings opened", on: announcement)
        }

        func testFediverseProviderFiltersExposeLabelsAndSelection() {
            let app = makeApplication()
            app.launchArguments = [
                "--ui-testing-fediverse",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launch()

            let buttons = Self.fediverseProviders.map { provider in
                let button = app.buttons[provider.identifier]
                XCTAssertTrue(button.waitForExistence(timeout: 20), "Missing \(provider.label) provider filter")
                XCTAssertEqual(button.label, provider.label)
                return button
            }

            XCTAssertTrue(buttons[0].isSelected)
            for (index, button) in buttons.dropFirst().enumerated() {
                button.click()
                XCTAssertTrue(button.isSelected)
                XCTAssertFalse(buttons[index].isSelected)
            }
        }
    #endif

    private func makeApplication() -> XCUIApplication {
        let app = XCUIApplication()
        app.launchArguments.append(Self.isolatedUITestingArgument)
        app.launchEnvironment["VOUCHA_API_BASE_URL"] = Self.isolatedAPIBaseURL
        app.launchEnvironment[Self.disabledAppAttestEnvironmentKey] = "1"
        return app
    }

    private func waitForLabel(_ expectedLabel: String, on element: XCUIElement) {
        let expectation = XCTNSPredicateExpectation(
            predicate: NSPredicate(format: "label == %@", expectedLabel),
            object: element
        )
        XCTAssertEqual(XCTWaiter().wait(for: [expectation], timeout: 20), .completed)
    }
}
