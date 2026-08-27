import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class DirectMessagesParticipantLocalizationTests: XCTestCase {
    func testParticipantRolesRenderInSpanishAndFrench() {
        XCTAssertEqual(
            directMessageParticipantRoleLabel("owner", locale: Locale(identifier: "es")),
            "Propietario"
        )
        XCTAssertEqual(
            directMessageParticipantRoleLabel("moderator", locale: Locale(identifier: "fr")),
            "Modérateur"
        )
    }
}
