import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

final class AndroidAuthenticationPresentationTests: XCTestCase {
    @MainActor
    func testExpiredOrFailedTurnstileChallengeClearsPreviouslyAcceptedToken() throws {
        let account = AndroidAccount(cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()))
        account.turnstileTokenDidChange("valid-token")
        XCTAssertEqual(account.turnstileToken, "valid-token")

        account.turnstileTokenDidChange(nil)

        XCTAssertNil(account.turnstileToken)
        let error = try XCTUnwrap(account.authError)
        XCTAssertEqual(error, .message(.nativeAuthVerificationTokenRequired))
        for language in ["en", "fr"] {
            let locale = Locale(identifier: language)
            XCTAssertEqual(
                UiMessages.string(error, locale: locale),
                UiMessages.string(.nativeAuthVerificationTokenRequired, locale: locale)
            )
        }
        account.turnstileTokenDidChange("replacement-token")
        XCTAssertEqual(account.turnstileToken, "replacement-token")
        XCTAssertNil(account.authError)
    }

    @MainActor
    func testFailedChallengeCanRetryWithoutAcceptingAnOlderCallback() {
        let account = AndroidAccount(cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()))
        let failedID = account.turnstileChallengeID
        account.turnstileTokenDidChange(nil, challengeID: failedID)
        XCTAssertNotNil(account.authError)
        XCTAssertEqual(account.turnstileChallengeID, failedID)

        account.retryTurnstileChallenge()

        XCTAssertNotEqual(account.turnstileChallengeID, failedID)
        XCTAssertNil(account.authError)
        account.turnstileTokenDidChange("stale-token", challengeID: failedID)
        XCTAssertNil(account.turnstileToken)
        account.turnstileTokenDidChange("fresh-token", challengeID: account.turnstileChallengeID)
        XCTAssertEqual(account.turnstileToken, "fresh-token")
        account.isAuthenticating = true
        let activeID = account.turnstileChallengeID
        account.retryTurnstileChallenge()
        XCTAssertEqual(account.turnstileChallengeID, activeID)
        XCTAssertEqual(account.turnstileToken, "fresh-token")
    }

    func testChallengeMarkupEscapesConfigurationAndHandlesScriptLoadFailureIndependently() throws {
        let html = try XCTUnwrap(try androidTurnstileHTML(
            "a\"<&",
            actionTag: "auth.email-address-tokens",
            baseURL: XCTUnwrap(URL(string: "https://staging.example/api"))
        ))
        XCTAssertTrue(html.contains("data-sitekey=\"a&quot;&lt;&amp;\""))
        XCTAssertTrue(html.contains("data-action=\"auth-email-address-tokens\""))
        XCTAssertTrue(html.contains("src=\"https://challenges.cloudflare.com/turnstile/v0/api.js\""))
        XCTAssertTrue(html.contains("onerror=\"location.href='voucha://turnstile?error'\""))
        XCTAssertFalse(html.contains("onerror=\"tokenError()\""))
    }

    func testInvalidChallengeContextProducesNoMarkup() throws {
        let baseURL = try XCTUnwrap(URL(string: "https://staging.example"))
        XCTAssertNil(androidTurnstileHTML("key", actionTag: "posts.<create>", baseURL: baseURL))
        XCTAssertNil(try androidTurnstileHTML(
            "key",
            actionTag: "posts.create",
            baseURL: XCTUnwrap(URL(string: "file:///tmp"))
        ))
    }

}
