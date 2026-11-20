import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

final class NotificationTextPostsTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    func testPostNewTitleFollowsPostTypeAndUsesSnippetBody() throws {
        let expected = [
            "review": "New review from @alice",
            "discussion": "New discussion from @alice",
            "data_point": "New data point from @alice",
            "link": "New link from @alice",
            "story": "New post from @alice"
        ]
        for (type, title) in expected {
            let rendered = try Support.render(
                "post_new",
                fields: ["post_id": "p1"],
                hydration: ["actors": Support.actors(), "posts": Support.post(type: type)]
            )
            XCTAssertEqual(rendered, RenderedNotification(title: title, body: "Hello world"), type)
        }
    }

    func testPostNewAnonymousTitleWhenActorOrPostIsMissing() throws {
        let anonymousPost = try Support.render(
            "post_new",
            fields: ["post_id": "p1"],
            hydration: ["posts": Support.post(type: "review", actor: nil)]
        )
        XCTAssertEqual(anonymousPost, RenderedNotification(title: "New review", body: "Hello world"))
        let goneActor = try Support.render(
            "post_new",
            fields: ["post_id": "p1"],
            hydration: ["posts": Support.post(type: "link")]
        )
        XCTAssertEqual(goneActor.title, "New link")
        let missingPost = try Support.render("post_new", fields: ["post_id": "gone"])
        XCTAssertEqual(missingPost, RenderedNotification(title: "New post", body: nil))
        let emptySnippet = try Support.render(
            "post_new",
            fields: ["post_id": "p1"],
            hydration: ["posts": Support.post(snippet: "")]
        )
        XCTAssertNil(emptySnippet.body)
    }

    func testPostManualSendUsesSenderNotPostActor() throws {
        let senders: [String: Any] = Support.actors("u3", "carol")
        let expected = [
            "review": "review", "discussion": "discussion", "data_point": "data point",
            "link": "link", "story": "post"
        ]
        for (type, noun) in expected {
            let fields: [String: Any] = ["post_id": "p1", "sent_by_id": "u3"]
            let sent = try Support.render(
                "post_manual_send",
                fields: fields,
                hydration: ["actors": senders, "posts": Support.post(type: type)]
            )
            XCTAssertEqual(sent, RenderedNotification(title: "@carol sent you a \(noun)", body: "Hello world"), type)
            let anonymous = try Support.render(
                "post_manual_send",
                fields: fields,
                hydration: ["posts": Support.post(type: type)]
            )
            XCTAssertEqual(anonymous.title, "Someone sent you a \(noun)", type)
        }
        let missingPost = try Support.render("post_manual_send", fields: ["sent_by_id": "u3"])
        XCTAssertEqual(missingPost, RenderedNotification(title: "Someone sent you a post", body: nil))
    }

    func testPostReplyAndRemoved() throws {
        let reply = try Support.render(
            "post_reply",
            fields: ["post_id": "p1"],
            hydration: ["actors": Support.actors(), "posts": Support.post(snippet: "Nice")]
        )
        XCTAssertEqual(reply, RenderedNotification(title: "New reply from @alice", body: "Nice"))
        let anonymous = try Support.render("post_reply")
        XCTAssertEqual(anonymous, RenderedNotification(title: "New reply", body: nil))
        let removed = try Support.render("post_removed")
        XCTAssertEqual(removed, RenderedNotification(title: "Your post has been removed", body: nil))
    }

    func testRssItemKinds() throws {
        let feed: [String: Any] = ["r1": ["id": "r1", "feed_name": "Hacker Feed", "snippet": "Story"]]
        let new = try Support.render(
            "rss_item_new",
            fields: ["rss_feed_item_id": "r1"],
            hydration: ["rss_feed_items": feed]
        )
        XCTAssertEqual(new, RenderedNotification(title: "New item from Hacker Feed", body: "Story"))
        let fallback = try Support.render("rss_item_new", fields: ["rss_feed_item_id": "gone"])
        XCTAssertEqual(fallback, RenderedNotification(title: "New item from RSS Feed", body: nil))
        let manual = try Support.render(
            "rss_item_manual_send",
            fields: ["rss_feed_item_id": "r1", "sent_by_id": "u3"],
            hydration: ["rss_feed_items": feed, "actors": Support.actors("u3", "carol")]
        )
        XCTAssertEqual(manual, RenderedNotification(title: "@carol sent you an article", body: "Story"))
        let anonymous = try Support.render("rss_item_manual_send", fields: ["sent_by_id": "u3"])
        XCTAssertEqual(anonymous, RenderedNotification(title: "Someone sent you an article", body: nil))
    }
}

final class NotificationTextSocialTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    func testFollowKindsUseActorUsername() throws {
        let hydration: [String: Any] = ["actors": Support.actors()]
        let fields: [String: Any] = ["actor_user_id": "u2"]
        let follow = try Support.render("follow", fields: fields, hydration: hydration)
        XCTAssertEqual(follow, RenderedNotification(title: "@alice started following you", body: nil))
        let referral = try Support.render("follow_referral_signup", fields: fields, hydration: hydration)
        XCTAssertEqual(referral.title, "Your referral @alice signed up and followed you!")
        XCTAssertEqual(try Support.render("follow").title, "Someone started following you")
        XCTAssertEqual(
            try Support.render("follow_referral_signup").title,
            "Your referral signed up and followed you!"
        )
    }

    func testReferralSignupChoosesSingularOrPluralBody() throws {
        let hydration: [String: Any] = ["actors": Support.actors()]
        let fields: [String: Any] = ["actor_user_id": "u2"]
        let one = try Support.render(
            "referral_signup",
            params: ["referral_count": 1],
            fields: fields,
            hydration: hydration
        )
        XCTAssertEqual(
            one,
            RenderedNotification(title: "@alice signed up through your referral!", body: "You now have 1 referral")
        )
        let many = try Support.render("referral_signup", params: ["referral_count": 3], fields: fields)
        XCTAssertEqual(
            many,
            RenderedNotification(title: "Someone signed up through your referral!", body: "You now have 3 referrals")
        )
        let none = try Support.render("referral_signup")
        XCTAssertEqual(none.body, "You now have 0 referrals")
    }

    func testTitleOnlyAndFixedKinds() throws {
        XCTAssertEqual(
            try Support.render("referral_click"),
            RenderedNotification(title: "Someone clicked your referral link", body: nil)
        )
        XCTAssertEqual(
            try Support.render("direct_message"),
            RenderedNotification(title: "New direct message", body: nil)
        )
        XCTAssertEqual(try Support.render("modmail"), RenderedNotification(title: "New modmail message", body: nil))
        XCTAssertEqual(
            try Support.render("household_invitation"),
            RenderedNotification(title: "Household invitation", body: "You have been invited to join a household.")
        )
    }

    func testEveryKnownKindHasARenderer() {
        for kind in NotificationKind.allCases where kind != .unknown {
            XCTAssertNotNil(NotificationTextRenderer.renderers[kind], kind.rawValue)
        }
        XCTAssertNil(NotificationTextRenderer.renderers[.unknown])
    }

    func testUnknownKindFallsBackToGenericTitleOnly() throws {
        let rendered = try Support.render("brand_new_future_kind")
        XCTAssertEqual(rendered, RenderedNotification(title: "New notification", body: nil))
    }
}
