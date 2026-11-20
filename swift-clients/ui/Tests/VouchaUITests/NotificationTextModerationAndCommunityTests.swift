import Foundation
import XCTest

final class NotificationTextModerationTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    func testFixedModerationKinds() throws {
        XCTAssertEqual(
            try Support.render("report_reviewed"),
            RenderedNotification(title: "Your report was reviewed", body: "Thanks for helping keep Voucha safe.")
        )
        XCTAssertEqual(
            try Support.render("critical_moderation_alert"),
            RenderedNotification(title: "Critical moderation alert", body: nil)
        )
        let removed = try Support.render("review_actioned_removed")
        XCTAssertEqual(removed.title, "Your review has been actioned by a moderator")
        XCTAssertTrue(try XCTUnwrap(removed.body).hasPrefix("A moderator has removed your review"))
        let note = try Support.render("review_actioned_note")
        XCTAssertEqual(note.body, "A moderator has added a contextual note to your review following a dispute.")
        let ban = try Support.render("community_ban")
        XCTAssertEqual(ban.title, "You have been banned from a community")
        XCTAssertTrue(try XCTUnwrap(ban.body).hasPrefix("A moderator has banned you"))
    }

    func testResolutionBodiesComeFromPublicTextsOrAreTitleOnly() throws {
        let dispute = try Support.render(
            "review_dispute_resolved",
            fields: ["review_dispute_id": "d1"],
            hydration: ["public_texts": ["d1": "Upheld after review."]]
        )
        XCTAssertEqual(
            dispute,
            RenderedNotification(title: "Your review dispute has been resolved", body: "Upheld after review.")
        )
        let appeal = try Support.render(
            "moderation_appeal_resolved",
            fields: ["moderation_appeal_id": "a1"],
            hydration: ["public_texts": ["a1": "Appeal denied."]]
        )
        XCTAssertEqual(
            appeal,
            RenderedNotification(title: "Your moderation appeal has been resolved", body: "Appeal denied.")
        )
        XCTAssertNil(try Support.render("review_dispute_resolved", fields: ["review_dispute_id": "d1"]).body)
        XCTAssertNil(try Support.render("moderation_appeal_resolved").body)
        let empty = try Support.render(
            "moderation_appeal_resolved",
            fields: ["moderation_appeal_id": "a1"],
            hydration: ["public_texts": ["a1": ""]]
        )
        XCTAssertNil(empty.body)
    }

    func testUserWarningPrefersPublicTextThenFallsBack() throws {
        let sent = try Support.render(
            "user_warning",
            fields: ["user_warning_id": "w1"],
            hydration: ["public_texts": ["w1": "Please be kind."]]
        )
        XCTAssertEqual(sent, RenderedNotification(title: "You received a warning", body: "Please be kind."))
        let fallback = try Support.render("user_warning", fields: ["user_warning_id": "w1"])
        XCTAssertEqual(fallback.body, "A moderator has issued a warning to your account.")
    }
}

final class NotificationTextCommunityTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport
    private let communityFields: [String: Any] = ["community_id": "c1"]

    func testApprovedAndOwnershipKindsInterpolateCommunityInTitleAndBody() throws {
        let hydration: [String: Any] = ["communities": Support.community()]
        let approved = try Support.render(
            "community_application_approved",
            fields: communityFields,
            hydration: hydration
        )
        XCTAssertEqual(approved, RenderedNotification(
            title: "Your application to Builders was approved",
            body: "You are now a member of Builders."
        ))
        let received = try Support.render("community_ownership_received", fields: communityFields, hydration: hydration)
        XCTAssertEqual(received, RenderedNotification(
            title: "You now own Builders",
            body: "Ownership of Builders was transferred to you."
        ))
        let away = try Support.render(
            "community_ownership_transferred_away",
            fields: communityFields,
            hydration: hydration
        )
        XCTAssertEqual(away, RenderedNotification(
            title: "Ownership of Builders was transferred",
            body: "You transferred ownership of Builders."
        ))
    }

    func testMissingCommunityFallsBackToCatalogName() throws {
        let gone = try Support.render("community_application_approved", fields: communityFields)
        XCTAssertEqual(gone.title, "Your application to a community was approved")
        let noId = try Support.render("community_ownership_received")
        XCTAssertEqual(noId.title, "You now own a community")
    }

    func testRejectedUsesReasonWhenSentAndGenericBodyOtherwise() throws {
        let withReason = try Support.render(
            "community_application_rejected",
            fields: ["community_id": "c1", "community_application_id": "ca1"],
            hydration: ["communities": Support.community(), "public_texts": ["ca1": "Not a fit."]]
        )
        XCTAssertEqual(withReason, RenderedNotification(
            title: "Your application to Builders was not approved",
            body: "Reason: Not a fit."
        ))
        let without = try Support.render(
            "community_application_rejected",
            fields: ["community_id": "c1", "community_application_id": "ca1"],
            hydration: ["communities": Support.community()]
        )
        XCTAssertEqual(without.body, "Your application to Builders was not approved.")
    }

    func testRoleChangedMapsRolesWithMemberDefault() throws {
        let hydration: [String: Any] = ["communities": Support.community()]
        let promoted = try Support.render(
            "community_role_changed",
            params: ["from_role": "member", "to_role": "moderator"],
            fields: communityFields,
            hydration: hydration
        )
        XCTAssertEqual(promoted, RenderedNotification(
            title: "Your role in Builders changed",
            body: "Your community role changed from member to moderator."
        ))
        let owner = try Support.render("community_role_changed", params: ["from_role": "moderator", "to_role": "owner"])
        XCTAssertEqual(owner.body, "Your community role changed from moderator to owner.")
        let unknown = try Support.render("community_role_changed", params: ["from_role": "banned"])
        XCTAssertEqual(unknown.body, "Your community role changed from member to member.")
    }
}
