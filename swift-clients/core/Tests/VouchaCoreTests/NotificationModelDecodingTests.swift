import Foundation
@testable import VouchaModels
import XCTest

final class VouchaNotificationDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        d.dateDecodingStrategy = .iso8601
        return d
    }()

    private func json(
        id: String = "n1",
        entityType: String = "post",
        kind: String = "post_new",
        params: String = "{}",
        extra: String = ""
    ) -> Data {
        Data("""
        {"id":"\(id)","user_id":"u1","entity_type":"\(entityType)","kind":"\(kind)","params":\(params),
        "post_id":"p1","rss_feed_item_id":null,"actor_user_id":"u2","sent_by_id":"u3","community_id":null,
        "conversation_id":null,"moderation_report_id":null,"review_dispute_id":null,"user_warning_id":null,
        "moderation_appeal_id":"a1","community_application_id":"ca1","copyright_notice_id":null,
        "copyright_notice_delivery_work_item_id":"w1","top_post_id":"tp1","household_id":null,
        "event_key":null,"target_path":"/posts/p1","target_entity":null,"target_intent":null,"read_at":null,
        "pushed_at":null,"created_at":"2024-01-15T10:00:00Z","updated_at":"2024-01-15T10:00:00Z"\(extra)}
        """.utf8)
    }

    func testDecodesKindParamsAndRelationIds() throws {
        let n = try decoder.decode(VouchaNotification.self, from: json(params: #"{"referral_count":3}"#))
        XCTAssertEqual(n.id, "n1")
        XCTAssertEqual(n.entityType, .post)
        XCTAssertEqual(n.kind, .postNew)
        XCTAssertEqual(n.postId, "p1")
        XCTAssertEqual(n.actorUserId, "u2")
        XCTAssertEqual(n.sentById, "u3")
        XCTAssertEqual(n.moderationAppealId, "a1")
        XCTAssertEqual(n.communityApplicationId, "ca1")
        XCTAssertEqual(n.copyrightNoticeDeliveryWorkItemId, "w1")
        XCTAssertEqual(n.topPostId, "tp1")
        XCTAssertEqual(n.targetPath, "/posts/p1")
        XCTAssertEqual(n.params.number("referral_count"), 3)
        XCTAssertNil(n.readAt)
    }

    func testEveryKindRawValueRoundTrips() throws {
        for kind in NotificationKind.allCases where kind != .unknown {
            let n = try decoder.decode(VouchaNotification.self, from: json(kind: kind.rawValue))
            XCTAssertEqual(n.kind, kind)
        }
        XCTAssertEqual(NotificationKind.allCases.count, 36)
    }

    func testUnknownKindAndEntityTypeFallBackToUnknown() throws {
        let n = try decoder.decode(
            VouchaNotification.self,
            from: json(entityType: "future_type_xyz", kind: "future_kind_xyz")
        )
        XCTAssertEqual(n.entityType, .unknown)
        XCTAssertEqual(n.kind, .unknown)
    }

    func testDecodesKnownEntityTypes() throws {
        let ban = try decoder.decode(VouchaNotification.self, from: json(entityType: "community_ban"))
        XCTAssertEqual(ban.entityType, .communityBan)
        let rss = try decoder.decode(VouchaNotification.self, from: json(entityType: "rss_feed_item"))
        XCTAssertEqual(rss.entityType, .rssFeedItem)
        XCTAssertNil(rss.rssFeedItemId)
    }

    func testRemovedCopyFieldsAreNoLongerRequired() throws {
        let withLegacy = json(extra: #","title":"Old","body":"Old","actor_label":null"#)
        XCTAssertNoThrow(try decoder.decode(VouchaNotification.self, from: withLegacy))
    }

    func testMissingRequiredKindFails() {
        let data = Data(#"{"id":"n1"}"#.utf8)
        XCTAssertThrowsError(try decoder.decode(VouchaNotification.self, from: data))
    }
}

final class NotificationParamsTests: XCTestCase {
    private func decode(_ json: String) throws -> NotificationParams {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(NotificationParams.self, from: Data(json.utf8))
    }

    func testDecodesStringNumberAndBoolValues() throws {
        let params = try decode(#"{"ai_assisted":true,"top_discussion_replies":4,"from_role":"owner","n":"7"}"#)
        XCTAssertTrue(params.bool("ai_assisted"))
        XCTAssertEqual(params.number("top_discussion_replies"), 4)
        XCTAssertEqual(params.string("from_role"), "owner")
        XCTAssertEqual(params["from_role"], .string("owner"))
        XCTAssertEqual(params.number("n"), 7)
    }

    func testLookupsAcceptSnakeAndCamelKeys() {
        let camel = NotificationParams(["referralCount": .number(2)])
        let snake = NotificationParams(["referral_count": .number(5)])
        XCTAssertEqual(camel.number("referral_count"), 2)
        XCTAssertEqual(snake.number("referral_count"), 5)
    }

    func testCoercionsMirrorTheWebNumberSemantics() {
        let params = NotificationParams([
            "flag": .bool(true), "off": .bool(false), "bad": .string("abc"), "text": .string("x")
        ])
        XCTAssertEqual(params.number("flag"), 1)
        XCTAssertEqual(params.number("off"), 0)
        XCTAssertEqual(params.number("bad"), 0)
        XCTAssertEqual(params.number("missing"), 0)
        XCTAssertNil(params.string("flag"))
        XCTAssertEqual(params.string("text"), "x")
        XCTAssertFalse(params.bool("text"))
        XCTAssertFalse(params.bool("off"))
        XCTAssertFalse(params.bool("missing"))
        XCTAssertEqual(NotificationParams()["_"], nil)
    }

    func testEncodesBackToWireValues() throws {
        let params = NotificationParams(["a": .string("s"), "b": .number(2), "c": .bool(true)])
        let data = try JSONEncoder().encode(params)
        XCTAssertEqual(try decode(String(decoding: data, as: UTF8.self)), params)
    }
}

final class NotificationHydrationTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    func testAbsentMapsDecodeEmpty() throws {
        let hydration = try decoder.decode(NotificationHydration.self, from: Data("{}".utf8))
        XCTAssertTrue(hydration.actors.isEmpty)
        XCTAssertTrue(hydration.posts.isEmpty)
        XCTAssertTrue(hydration.publicTexts.isEmpty)
    }

    func testDecodesEveryHydrationMap() throws {
        let first = try decoder.decode(NotificationHydration.self, from: Data("""
        {"actors":{"u1":{"id":"u1","username":"old"}},
         "communities":{"c1":{"id":"c1","slug":"s","name":"Old"}},
         "posts":{"p1":{"id":"p1","post_type":"review","snippet":"hi","actor_user_id":null,"community_id":"c1"}},
         "rss_feed_items":{"r1":{"id":"r1","feed_name":"Feed","snippet":"s"}},
         "copyright_notices":{"k1":{"id":"k1","jurisdiction":"eu_dsa"}},
         "public_texts":{"t1":"text"}}
        """.utf8))
        XCTAssertEqual(first.posts["p1"]?.communityId, "c1")
        XCTAssertNil(first.posts["p1"]?.actorUserId)
        XCTAssertEqual(first.rssFeedItems["r1"]?.feedName, "Feed")
        XCTAssertEqual(first.copyrightNotices["k1"]?.jurisdiction, "eu_dsa")
        XCTAssertEqual(first.actors["u1"]?.username, "old")
        XCTAssertEqual(first.communities["c1"]?.name, "Old")
        XCTAssertEqual(first.publicTexts, ["t1": "text"])
    }
}
