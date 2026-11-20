import Foundation
import XCTest

final class NotificationTextDigestTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    private let params: [String: Any] = [
        "community_count": 1, "joins": 2, "departures": 5, "total_members": 10, "active_posters": 3,
        "discussions": 4, "reviews": 5, "data_points": 6, "comments": 7, "moderation_workload": 8,
        "top_discussion_replies": 9
    ]

    func testDigestComputesNetAndRateWithSingularCommunity() throws {
        let rendered = try Support.render("community_activity_digest", params: params)
        XCTAssertEqual(rendered.title, "Your weekly community activity")
        XCTAssertEqual(
            rendered.body,
            "1 community: 2 joined, 5 departed (net -3); 10 total members; 4 discussions, 5 reviews, " +
                "6 data points, 7 comments; 3 active members (30%); 8 items awaiting moderation."
        )
    }

    func testDigestPluralCommunitiesRoundedRateAndZeroTotal() throws {
        var plural = params
        plural["community_count"] = 3
        plural["total_members"] = 3
        plural["active_posters"] = 1
        let many = try Support.render("community_activity_digest", params: plural)
        XCTAssertTrue(try XCTUnwrap(many.body).hasPrefix("3 communities: "))
        XCTAssertTrue(try XCTUnwrap(many.body).contains("1 active members (33%)"))
        var empty = params
        empty["total_members"] = 0
        let zero = try Support.render("community_activity_digest", params: empty)
        XCTAssertTrue(try XCTUnwrap(zero.body).contains("(0%)"))
        let bare = try Support.render("community_activity_digest")
        XCTAssertEqual(
            bare.body,
            "0 communities: 0 joined, 0 departed (net 0); 0 total members; 0 discussions, 0 reviews, " +
                "0 data points, 0 comments; 0 active members (0%); 0 items awaiting moderation."
        )
    }

    func testDigestAppendsTopDiscussionWhenPostAndCommunityAreHydrated() throws {
        let hydration: [String: Any] = [
            "posts": Support.post("tp1", snippet: "Best tools", community: "c1"),
            "communities": Support.community()
        ]
        let rendered = try Support.render(
            "community_activity_digest",
            params: params,
            fields: ["top_post_id": "tp1"],
            hydration: hydration
        )
        XCTAssertTrue(try XCTUnwrap(rendered.body).hasSuffix("; top discussion: Builders: Best tools (9 replies)."))
    }

    func testDigestOmitsTopDiscussionWhenPostOrCommunityIsMissing() throws {
        let noPost = try Support.render("community_activity_digest", params: params, fields: ["top_post_id": "tp1"])
        XCTAssertFalse(try XCTUnwrap(noPost.body).contains("top discussion"))
        let noCommunity = try Support.render(
            "community_activity_digest",
            params: params,
            fields: ["top_post_id": "tp1"],
            hydration: ["posts": Support.post("tp1", community: "c1")]
        )
        XCTAssertFalse(try XCTUnwrap(noCommunity.body).contains("top discussion"))
        let uncommunityPost = try Support.render(
            "community_activity_digest",
            params: params,
            fields: ["top_post_id": "tp1"],
            hydration: ["posts": Support.post("tp1"), "communities": Support.community()]
        )
        XCTAssertFalse(try XCTUnwrap(uncommunityPost.body).contains("top discussion"))
    }
}

final class NotificationTextCopyrightTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    private let fixed: [(kind: String, title: String, body: String)] = [
        ("copyright_claimant_receipt", "Copyright notice received", "Your copyright notice was received."),
        ("copyright_status_update", "Copyright case update", "There is an update to your copyright case."),
        (
            "copyright_poster_restriction_notice", "Material restricted for a copyright notice",
            "Review the case and available response options."
        ),
        (
            "copyright_poster_review_notice", "Copyright restriction reviewed",
            "A person reviewed the restriction. See the case page for the decision and reasons."
        ),
        (
            "copyright_poster_restoration_notice", "Copyright restriction ended",
            "This restriction ended. See the case page for the image availability and reasons."
        ),
        (
            "copyright_owner_information_notice", "Community image restricted",
            "An image on a community you own was withheld after a copyright notice."
        ),
        (
            "copyright_redress_decision_notice", "Copyright complaint decision",
            "A person decided your complaint. See the case page for the disposition and reasons."
        )
    ]

    func testFixedTitleWithFallbackBody() throws {
        for item in fixed {
            let rendered = try Support.render(item.kind)
            XCTAssertEqual(rendered, RenderedNotification(title: item.title, body: item.body), item.kind)
        }
    }

    func testDeliveredSummaryReplacesFallbackBody() throws {
        for item in fixed {
            let rendered = try Support.render(
                item.kind,
                fields: ["copyright_notice_delivery_work_item_id": "w1"],
                hydration: ["public_texts": ["w1": "Summary text."]]
            )
            XCTAssertEqual(rendered, RenderedNotification(title: item.title, body: "Summary text."), item.kind)
        }
    }

    func testAiAssistedAppendsDisclosureAfterBlankLine() throws {
        let note = "Automated tools assisted with processing this case."
        let fallback = try Support.render("copyright_status_update", params: ["ai_assisted": true])
        XCTAssertEqual(fallback.body, "There is an update to your copyright case.\n\n\(note)")
        let summary = try Support.render(
            "copyright_claimant_receipt",
            params: ["ai_assisted": true],
            fields: ["copyright_notice_delivery_work_item_id": "w1"],
            hydration: ["public_texts": ["w1": "Summary text."]]
        )
        XCTAssertEqual(summary.body, "Summary text.\n\n\(note)")
        let notAssisted = try Support.render("copyright_status_update", params: ["ai_assisted": false])
        XCTAssertEqual(notAssisted.body, "There is an update to your copyright case.")
        let wrongType = try Support.render("copyright_status_update", params: ["ai_assisted": "true"])
        XCTAssertEqual(wrongType.body, "There is an update to your copyright case.")
    }

    func testClaimantDecisionFallbackDependsOnJurisdiction() throws {
        func render(_ jurisdiction: String?, origin: URL = Support.webOrigin) throws -> RenderedNotification {
            var hydration: [String: Any] = [:]
            if let jurisdiction {
                hydration["copyright_notices"] = ["k1": ["id": "k1", "jurisdiction": jurisdiction]]
            }
            return try Support.render(
                "copyright_claimant_decision_notice",
                fields: ["copyright_notice_id": "k1"],
                hydration: hydration,
                webOrigin: origin
            )
        }
        let us = try render("us_dmca")
        XCTAssertEqual(us.title, "Copyright notice decision")
        XCTAssertEqual(
            us.body,
            "Your copyright notice was decided. You may file a new notice, contact the designated agent, or " +
                "seek judicial redress through a court. New notice: https://web.example.test/copyright/notices/new. " +
                "Designated agent: https://web.example.test/copyright/designated-agent. " +
                "Accepted cases show the reasons on the case page."
        )
        XCTAssertTrue(try XCTUnwrap(render("eu_dsa").body).contains("certified out-of-court dispute settlement body"))
        let other = "Your copyright notice was decided. The decision statement gives the reasons. " +
            "You may seek judicial redress through a court."
        XCTAssertEqual(try render("other_regime").body, other)
        XCTAssertEqual(try render(nil).body, other)
        XCTAssertEqual(try Support.render("copyright_claimant_decision_notice").body, other)
        let configured = try render("us_dmca", origin: XCTUnwrap(URL(string: "https://staging.example.test/")))
        XCTAssertTrue(try XCTUnwrap(configured.body)
            .contains("https://staging.example.test/copyright/designated-agent"))
    }

    func testClaimantDecisionSummaryWinsOverJurisdictionFallback() throws {
        let rendered = try Support.render(
            "copyright_claimant_decision_notice",
            params: ["ai_assisted": true],
            fields: ["copyright_notice_id": "k1", "copyright_notice_delivery_work_item_id": "w1"],
            hydration: [
                "copyright_notices": ["k1": ["id": "k1", "jurisdiction": "us_dmca"]],
                "public_texts": ["w1": "Decided in your favor."]
            ]
        )
        XCTAssertEqual(
            rendered.body,
            "Decided in your favor.\n\nAutomated tools assisted with processing this case."
        )
    }
}
