#if os(iOS)
    import UIKit
    import XCTest

    final class SignInFlowTests: LaunchSmokeTestCase {
        private let email = "ui-test@example.com"
        private let code = "ABCDEFGH23"
        private let turnstileToken = "ui-testing-turnstile-token"

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testEmailOTPSignInReplacesTheSignedOutControl() throws {
            let fixtures = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = ["--ui-testing-sign-in", "-AppleLanguages", "(en)", "-AppleLocale", "en_US"]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_SIGN_IN_EMAIL"] = email
            app.launchEnvironment["VOUCHA_UI_TESTING_SIGN_IN_CODE"] = code
            app.launchEnvironment["VOUCHA_UI_TESTING_TURNSTILE_TOKEN"] = turnstileToken
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixtures.identity
            app.launchEnvironment["VOUCHA_UI_TESTING_OAUTH_PROVIDERS_RESPONSE_BASE64"] = fixtures.providers
            try LaunchSmokeFixtures.configureSession(in: app)
            app.launch()

            waitForLaunchReadiness(
                of: app.descendants(matching: .any)["root-shell"],
                "Sign-in launch did not mount the native root"
            )
            dismissSidebarIfPresented(in: app)
            let signIn = app.buttons["signed-out-sign-in"]
            XCTAssertTrue(signIn.waitForExistence(timeout: LaunchSmokeTiming.interaction))
            tapHittableButton(
                "Sign In",
                identifier: "signed-out-sign-in",
                in: app,
                timeout: LaunchSmokeTiming.interaction,
                requireUnique: true
            )

            let continueWithEmail = app.buttons["Continue with Email"]
            XCTAssertTrue(
                continueWithEmail.waitForExistence(timeout: LaunchSmokeTiming.interaction),
                "Sign-in sheet did not present. \(LaunchSmokeSystemBanner.describe(app: app)) "
                    + LaunchSmokeTiming.visibleText(in: app)
            )
            continueWithEmail.tap()

            let emailField = app.textFields["Email address"]
            XCTAssertTrue(emailField.waitForExistence(timeout: LaunchSmokeTiming.interaction))
            LaunchSmokeFieldTyping.type(email, into: emailField)
            tapHittableButton("Send Code", in: app)

            XCTAssertTrue(app.staticTexts["Check your email"].waitForExistence(timeout: LaunchSmokeTiming.interaction))
            let codeField = app.textFields["Verify Code"]
            XCTAssertTrue(codeField.waitForExistence(timeout: LaunchSmokeTiming.interaction))
            LaunchSmokeFieldTyping.type(code, into: codeField)
            tapHittableButton("Verify Code", in: app)

            let signedOutControlGone = signIn.waitForNonExistence(timeout: LaunchSmokeTiming.interaction)
            if !signedOutControlGone {
                XCTFail("Sign In remained. \(LaunchSmokeTiming.visibleText(in: app))")
            }
            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].exists)
        }

        private func dismissSidebarIfPresented(in app: XCUIApplication) {
            guard UIDevice.current.userInterfaceIdiom == .pad else { return }
            let hideSidebar = app.buttons["Hide Sidebar"]
            let showSidebar = app.buttons["Show Sidebar"]
            // Settle split navigation before using its detail-column sign-in control.
            var canHide = false
            let sidebarReady = pollThroughSnapshotStalls("Sidebar toggle wait") {
                canHide = hideSidebar.isHittableAndEnabled
                return canHide || showSidebar.isHittableAndEnabled
            }
            guard sidebarReady.satisfied, canHide else { return }
            // Hiding the sidebar is idempotent, so one lost tap is retried. On a starved runner
            // XCUITest can spend 30 s between the banner check and synthesizing the tap (run
            // 44126670756), long enough for a SpringBoard banner to take it.
            for attempt in 1 ... 2 {
                if app.state != .runningForeground {
                    app.activate()
                }
                tapHittableButton("Hide Sidebar", in: app, timeout: LaunchSmokeTiming.interaction)
                if hideSidebar.waitForNonExistence(timeout: LaunchSmokeTiming.interaction) {
                    return
                }
                let state = LaunchSmokeSystemBanner.describe(app: app)
                XCTContext.runActivity(named: "Hide Sidebar tap \(attempt) left the sidebar. \(state)") { _ in }
            }
            XCTFail(
                "Sidebar stayed after two Hide Sidebar taps. \(LaunchSmokeSystemBanner.describe(app: app)) "
                    + LaunchSmokeTiming.visibleText(in: app)
            )
        }

        @discardableResult
        private func tapHittableButton(
            _ label: String,
            identifier: String? = nil,
            in app: XCUIApplication,
            timeout: TimeInterval = LaunchSmokeTiming.interaction,
            requireUnique: Bool = false
        ) -> XCUIElement {
            let matches = identifier.map { app.buttons.matching(identifier: $0) }
                ?? app.buttons.matching(NSPredicate(format: "label == %@", label))
            var button: XCUIElement?
            // Probing isHittable on a frameless duplicate (e.g. an offscreen copy) fails the test
            // outright, so skip empty frames and only inspect every match when uniqueness matters.
            let actionable: (XCUIElement) -> Bool = {
                $0.label == label && !$0.frame.isEmpty && $0.isHittableAndEnabled
            }
            // One stalled snapshot must not use up the wait (CI run 45042621815).
            let ready = pollThroughSnapshotStalls("Hittable \(label) button wait") {
                let candidates = matches.allElementsBoundByIndex
                if requireUnique {
                    let hittable = candidates.filter(actionable)
                    button = hittable.count == 1 ? hittable.first : nil
                } else {
                    button = candidates.first(where: actionable)
                }
                return button != nil
            }
            XCTAssertNotNil(
                button, "No hittable \(label) in \(Int(LaunchSmokeTiming.hittability)) s (\(ready.stalls) stalls)"
            )
            // A SpringBoard banner would receive this tap instead of the button under it.
            toleratingSnapshotTimeouts("SpringBoard banner wait") {
                LaunchSmokeSystemBanner.waitUntilClear(timeout: timeout)
            }
            button?.tap()
            return button ?? app.buttons[label]
        }

        private func loadFixtures() throws -> (identity: String, providers: String) {
            let path = try XCTUnwrap(ProcessInfo.processInfo.environment["VOUCHA_CONTRACT_ROOT"])
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
                root: root,
                fixtures: fixtures
            )
            let providers = try fixtureData(
                id: "native.oauth.providers.broker-capabilities",
                path: "/api/v1/auth/oauth/providers",
                root: root,
                fixtures: fixtures
            )
            return (identity.base64EncodedString(), providers.base64EncodedString())
        }

        private func fixtureData(
            id: String, path: String, root: URL, fixtures: [[String: Any]]
        ) throws -> Data {
            let fixture = try XCTUnwrap(fixtures.first { $0["id"] as? String == id })
            XCTAssertEqual(fixture["method"] as? String, "GET")
            XCTAssertEqual(fixture["path"] as? String, path)
            XCTAssertEqual(fixture["status"] as? Int, 200)
            let bodyFile = try XCTUnwrap(fixture["bodyFile"] as? String)
            _ = try XCTUnwrap(bodyFile.hasPrefix("responses/") && !bodyFile.contains("..") ? bodyFile : nil)
            return try Data(contentsOf: root.appendingPathComponent(bodyFile))
        }
    }
#endif
