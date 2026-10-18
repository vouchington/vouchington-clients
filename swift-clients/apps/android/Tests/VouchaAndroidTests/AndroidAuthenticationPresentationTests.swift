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

}
