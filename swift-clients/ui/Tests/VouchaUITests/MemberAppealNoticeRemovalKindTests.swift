import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealNoticeRemovalKindTests: XCTestCase {
    func testRemovalNoticesLocalizePlatformAndCommunityKinds() throws {
        let cases: [(ModerationAppealPostRemovalKind, String)] = [
            (.platform, "Platform post removal appeal"),
            (.community, "Community post removal appeal")
        ]

        for (kind, expectedTitle) in cases {
            let card = MemberAppealNoticeCard(
                target: .removal(
                    id: "post-1",
                    title: "Removed post",
                    community: "Builders",
                    kind: kind,
                    date: .now
                ),
                canAppeal: true,
                onAppeal: {}
            )

            XCTAssertNoThrow(try card.inspect().find(text: expectedTitle))
        }
    }
}
