#if os(iOS)
    import XCTest

    final class SignInFlowTests: XCTestCase {
        private let email = "ui-test@example.com"
        private let code = "424242"
        private let turnstileToken = "ui-testing-turnstile-token"

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
            app.launch()

            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].waitForExistence(timeout: 20))
            let signIn = app.buttons["Sign In"]
            XCTAssertTrue(signIn.waitForExistence(timeout: 20))
            signIn.tap()

            let continueWithEmail = app.buttons["Continue with Email"]
            XCTAssertTrue(continueWithEmail.waitForExistence(timeout: 20))
            continueWithEmail.tap()

            let emailField = app.textFields["Email address"]
            XCTAssertTrue(emailField.waitForExistence(timeout: 20))
            emailField.tap()
            emailField.typeText(email)
            let sendCode = app.buttons["Send Code"]
            let enabled = NSPredicate(format: "isEnabled == true")
            let becameEnabled = expectation(for: enabled, evaluatedWith: sendCode)
            wait(for: [becameEnabled], timeout: 10)
            sendCode.tap()

            XCTAssertTrue(app.staticTexts["Check your email"].waitForExistence(timeout: 20))
            let codeField = app.textFields["6-digit code"]
            XCTAssertTrue(codeField.waitForExistence(timeout: 20))
            codeField.tap()
            codeField.typeText(code)
            app.buttons["Verify Code"].tap()

            XCTAssertTrue(signIn.waitForNonExistence(timeout: 20))
            XCTAssertTrue(app.descendants(matching: .any)["root-shell"].exists)
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
