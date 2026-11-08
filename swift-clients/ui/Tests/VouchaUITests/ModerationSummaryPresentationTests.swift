import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationSummaryPresentationTests: XCTestCase {
    func testAppealSummariesShowPublicContextAndDraftPresenceWithoutUnsentResponse() throws {
        let community = ["id": "community-1", "name": "Public community"]
        let contexts: [([String: Any]?, String?)] = [
            ([
                "type": "warning",
                "id": "warning-1",
                "public_message": "Public warning explanation",
                "community": community,
                "created_at": "2026-06-01T11:30:00Z"
            ], "Public warning explanation"),
            (
                [
                    "type": "community_ban",
                    "id": "ban-1",
                    "reason": "Public ban explanation",
                    "community": community,
                    "expires_at": NSNull(),
                    "created_at": "2026-06-01T11:30:00Z"
                ],
                "Public ban explanation"
            ),
            ([
                "type": "suspension",
                "id": "suspension-1",
                "reason": "Public suspension explanation",
                "created_at": "2026-06-01T11:30:00Z"
            ], "Public suspension explanation"),
            (nil, nil)
        ]
        for (context, expectedReason) in contexts {
            var object = try summaryObject()
            if let context {
                object["target_context"] = context
            } else {
                object["target_context"] = NSNull()
            }
            object["has_draft"] = true
            object["has_ai_draft"] = false
            object["public_response"] = "Unsent response must stay private"
            object["sent_at"] = NSNull()
            let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
                ModerationAppealSummary.self, from: JSONSerialization.data(withJSONObject: object)
            )
            let inspection = try StaffModerationSummaryRow(appeal: summary).inspect()
            XCTAssertNoThrow(try inspection.find(text: "appealing-reviewer"))
            XCTAssertNoThrow(try inspection.find(text: "Drafts"))
            if let expectedReason { XCTAssertNoThrow(try inspection.find(text: expectedReason)) }
            XCTAssertThrowsError(try inspection.find(text: "Unsent response must stay private"))
        }
    }

    func testSummaryActorFallsBackToDeletedUserWithoutExposingActorIdentity() throws {
        var object = try summaryObject()
        object["appellant"] = [
            "id": "00000000-0000-7000-8000-000000000999", "username": "   ",
            "verified_display_name": "  ", "profile_image_id": NSNull()
        ]
        let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationAppealSummary.self, from: JSONSerialization.data(withJSONObject: object)
        )
        let inspection = try StaffModerationSummaryRow(appeal: summary).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Deleted user"))
        let rendered = try inspection.findAll(ViewType.Text.self).map { try $0.string() }
        XCTAssertFalse(rendered.contains { $0.contains("00000000-0000-7000-8000-000000000999") })
    }

    private func summaryObject() throws -> [String: Any] {
        let data = ApiFixtureLoader.data("native.moderation.appeals.default")
        let response = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        return try XCTUnwrap((response["appeals"] as? [[String: Any]])?.first)
    }
}
