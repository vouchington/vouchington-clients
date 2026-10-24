#if os(macOS)
    import Foundation
    import XCTest

    final class MacOSEmailSignInTests: XCTestCase {
        private let email = "ui-test@example.com"
        private let code = "424242"
        private let turnstileToken = "ui-testing-turnstile-token"

        override func setUp() {
            super.setUp()
            continueAfterFailure = false
        }

        func testEmailOTPSignInReplacesTheSignedOutControl() throws {
            let fixtures = try loadFixtures()
            let app = XCUIApplication()
            app.launchArguments = [
                "--ui-testing-sign-in",
                "-AppleLanguages", "(en)",
                "-AppleLocale", "en_US"
            ]
            app.launchEnvironment["VOUCHA_API_BASE_URL"] = "https://ui-testing.voucha.invalid"
            app.launchEnvironment["VOUCHA_DISABLE_APP_ATTEST"] = "1"
            app.launchEnvironment["VOUCHA_UI_TESTING_SIGN_IN_EMAIL"] = email
            app.launchEnvironment["VOUCHA_UI_TESTING_SIGN_IN_CODE"] = code
            app.launchEnvironment["VOUCHA_UI_TESTING_TURNSTILE_TOKEN"] = turnstileToken
            app.launchEnvironment["VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"] = fixtures.identity
            app.launchEnvironment["VOUCHA_UI_TESTING_OAUTH_PROVIDERS_RESPONSE_BASE64"] = fixtures.providers
            app.launch()
            app.activate()

            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            clickEnabled("Sign In", in: app)
            clickEnabled("Continue with Email", in: app)
            LaunchSmokeFieldTyping.type(email, into: textField("Email address", in: app))
            clickEnabled("Send Code", in: app)

            XCTAssertTrue(app.staticTexts["Check your email"].waitForExistence(timeout: 20))
            LaunchSmokeFieldTyping.type(code, into: textField("6-digit code", in: app))
            clickEnabled("Verify Code", in: app)

            let signIn = app.buttons.matching(NSPredicate(format: "label == %@", "Sign In"))
            let gone = expectation(
                for: NSPredicate(format: "count == 0"),
                evaluatedWith: signIn
            )
            XCTAssertEqual(XCTWaiter().wait(for: [gone], timeout: 20), .completed)
            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].exists)
        }

        private func clickEnabled(_ label: String, in app: XCUIApplication) {
            let matches = app.buttons.matching(NSPredicate(format: "label == %@", label))
            let ready = NSPredicate { _, _ in
                matches.allElementsBoundByIndex.contains { $0.exists && $0.isEnabled }
            }
            wait(for: [expectation(for: ready, evaluatedWith: nil)], timeout: 20)
            guard let button = matches.allElementsBoundByIndex.first(where: { $0.exists && $0.isEnabled }) else {
                XCTFail("Missing enabled \(label)")
                return
            }
            if button.isHittable {
                button.click()
            } else {
                button.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
        }

        private func textField(_ label: String, in app: XCUIApplication) -> XCUIElement {
            let field = app.textFields.matching(
                NSPredicate(
                    format: "label == %@ OR placeholderValue == %@ OR value == %@",
                    label,
                    label,
                    label
                )
            ).firstMatch
            XCTAssertTrue(field.waitForExistence(timeout: 20), "Missing field \(label)")
            return field
        }

        private func loadFixtures() throws -> (identity: String, providers: String) {
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
