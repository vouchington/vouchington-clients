import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealContextTests: XCTestCase {
    func testStaffCardRendersTargetAndStaffContext() throws {
        let contexts: [(String, [String])] = [
            (
                #"{"type":"warning","id":"warning-1","public_message":"Warning message","community":{"id":"community-1","name":"Builders"},"created_at":"2026-07-01T09:00:00Z"}"#,
                ["Decision: Warning message", "Community: Builders"]
            ),
            (
                #"{"type":"community_ban","id":"ban-1","community":{"id":"community-1","name":"Builders"},"reason":"Ban reason","expires_at":null,"created_at":"2026-07-01T09:00:00Z"}"#,
                ["Decision: Ban reason", "Community: Builders"]
            ),
            (
                #"{"type":"post_removal","id":"post-1","title":"Removed post","kind":"community","community":{"id":"community-1","name":"Builders"},"public_reason":"Removal reason","decided_at":"2026-07-01T09:00:00Z"}"#,
                ["Post: Removed post", "Decision: Removal reason", "Community: Builders"]
            ),
            (
                #"{"type":"suspension","id":"suspension-1","reason":"Suspension reason","created_at":"2026-07-01T09:00:00Z"}"#,
                ["Decision: Suspension reason"]
            )
        ]

        for (targetContext, expectedTargetValues) in contexts {
            let appeal = try decodeAppeal(targetContext: targetContext)
            let viewModel = ModerationAppealsViewModel(
                client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
            )
            let inspection = try ModerationAppealCard(viewModel: viewModel, appeal: appeal).inspect()

            let staffValues = [
                "Appellant Appeal Appellant",
                "Original decision reason: Original reason",
                "Original decision by original-moderator"
            ]
            for value in staffValues + expectedTargetValues {
                XCTAssertNoThrow(try inspection.find(text: value))
            }
        }
    }

    private func decodeAppeal(targetContext: String) throws -> ModerationAppeal {
        let staffContext = #"{"appellant":{"id":"user-1","username":"appellant","verified_display_name":"Appeal Appellant","profile_image_id":null},"original_decision":{"actor":{"id":"moderator-1","username":"original-moderator","verified_display_name":null,"profile_image_id":null},"internal_reason":"Original reason"}}"#
        let json = ModerationAppealsTestSupport.appeal()
            .replacingOccurrences(
                of: #""is_overdue":true"#,
                with: #""is_overdue":true,"target_context":\#(targetContext),"staff_context":\#(staffContext)"#
            )
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationAppeal.self,
            from: Data(json.utf8)
        )
    }
}
