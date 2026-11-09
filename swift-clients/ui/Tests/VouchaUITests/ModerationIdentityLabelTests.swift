import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

final class ModerationIdentityLabelTests: XCTestCase {
    func testDisplayNamePrecedesUsername() {
        XCTAssertEqual(
            moderationIdentityLabel(displayName: "Alice Example", username: "alice", locale: .init(identifier: "en")),
            "Alice Example"
        )
        XCTAssertEqual(
            moderationIdentityLabel(displayName: " \n ", username: "alice", locale: .init(identifier: "en")),
            "alice"
        )
    }

    func testUnavailableIdentityUsesLocalizedCopy() {
        for language in ["en", "es", "fr", "pt"] {
            let locale = Locale(identifier: language)
            XCTAssertEqual(
                moderationIdentityLabel(displayName: nil, username: nil, locale: locale),
                UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)
            )
            XCTAssertEqual(
                moderationIdentityLabel(displayName: " ", username: "\n", locale: locale),
                UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)
            )
        }
    }
}
