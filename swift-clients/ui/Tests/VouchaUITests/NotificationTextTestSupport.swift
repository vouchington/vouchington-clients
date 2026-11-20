import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

struct RenderedNotification: Equatable {
    let title: String
    let body: String?
}

enum NotificationTextTestSupport {
    static let webOrigin = URL(string: "https://web.example.test")!

    /// The producer wire shape of a notification of `kind`; `fields` override the null defaults.
    static func notificationJSON(
        _ kind: String,
        params: [String: Any] = [:],
        fields: [String: Any] = [:]
    ) -> [String: Any] {
        var value: [String: Any] = [
            "__entity_type": "notification", "id": "n1", "user_id": "u1", "entity_type": "post",
            "kind": kind, "params": params, "created_at": "2026-01-01T00:00:00Z",
            "updated_at": "2026-01-01T00:00:00Z"
        ]
        for key in [
            "post_id", "rss_feed_item_id", "actor_user_id", "sent_by_id", "moderation_report_id",
            "review_dispute_id", "user_warning_id", "moderation_appeal_id", "conversation_id", "community_id",
            "community_application_id", "household_id", "copyright_notice_id",
            "copyright_notice_delivery_work_item_id", "top_post_id", "event_key", "target_path",
            "target_entity", "target_intent", "read_at", "pushed_at"
        ] {
            value[key] = NSNull()
        }
        value.merge(fields) { _, override in override }
        return value
    }

    static func notification(
        _ kind: String,
        params: [String: Any] = [:],
        fields: [String: Any] = [:]
    ) throws -> VouchaNotification {
        try APIClient.makeDecoder().decode(
            VouchaNotification.self,
            from: JSONSerialization.data(withJSONObject: notificationJSON(kind, params: params, fields: fields))
        )
    }

    /// A `/my/notifications` page holding one notification plus hydration maps.
    static func pageJSON(
        kind: String,
        id: String,
        hasNext: Bool = false,
        fields: [String: Any] = [:],
        hydration: [String: Any] = [:]
    ) -> Data {
        var page = hydration
        page["results"] = [["__entity_type": "notification", "id": id, "read_at": NSNull()]]
        page["page_info"] = ["has_next_page": hasNext, "end_cursor": (hasNext ? "next" : NSNull()) as Any]
        var merged = fields
        merged["id"] = id
        merged["actor_user_id"] = merged["actor_user_id"] ?? "u2"
        page["notifications"] = [id: notificationJSON(kind, fields: merged)]
        return try! JSONSerialization.data(withJSONObject: page)
    }

    static func hydration(_ json: [String: Any] = [:]) throws -> NotificationHydration {
        try APIClient.makeDecoder().decode(
            NotificationHydration.self,
            from: JSONSerialization.data(withJSONObject: json)
        )
    }

    static func render(
        _ kind: String,
        params: [String: Any] = [:],
        fields: [String: Any] = [:],
        hydration json: [String: Any] = [:],
        webOrigin: URL = webOrigin
    ) throws -> RenderedNotification {
        let content = try NotificationTextRenderer.render(
            notification(kind, params: params, fields: fields),
            hydration: hydration(json),
            webOrigin: webOrigin
        )
        return RenderedNotification(title: uiEnglish(content.title), body: content.body.map(uiEnglish))
    }

    static func actors(_ id: String = "u2", _ username: String = "alice") -> [String: Any] {
        [id: ["id": id, "username": username]]
    }

    static func community(_ id: String = "c1", _ name: String = "Builders") -> [String: Any] {
        [id: ["id": id, "slug": "builders", "name": name]]
    }

    static func post(
        _ id: String = "p1",
        type: String = "discussion",
        snippet: String = "Hello world",
        actor: String? = "u2",
        community: String? = nil
    ) -> [String: Any] {
        [id: [
            "id": id, "post_type": type, "snippet": snippet,
            "actor_user_id": actor ?? NSNull(), "community_id": community ?? NSNull()
        ] as [String: Any]]
    }
}
