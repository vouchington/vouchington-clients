import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

final class AndroidConversationMenuTitleTests: XCTestCase {
    func testBlankTitlesUseLocalizedNewConversationCopy() {
        for locale in [Locale(identifier: "en"), Locale(identifier: "es")] {
            let fallback = UiMessages.string(.nativeSwiftChatNewConversation, locale: locale)
            XCTAssertEqual(androidConversationMenuTitle("", locale: locale), fallback)
            XCTAssertEqual(androidConversationMenuTitle(" \n\t ", locale: locale), fallback)
        }
    }

    func testNonblankTitleRemainsUserContent() {
        let locale = Locale(identifier: "en")
        XCTAssertEqual(
            androidConversationMenuTitle("  Saved chat  ", locale: locale),
            UiMessages.string(.userContent("  Saved chat  "), locale: locale)
        )
    }
}
