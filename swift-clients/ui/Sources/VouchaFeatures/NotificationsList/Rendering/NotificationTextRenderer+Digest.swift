import Foundation
import VouchaLocalization
import VouchaModels

extension NotificationTextRenderer {
    static func communityActivityDigest(_ context: NotificationTextContext) -> NotificationContent {
        var texts: [String: UiVerbatimText] = ["communities": digestCommunities(context.params)]
        let title = UiVerbatimText.message(.notificationsKindCommunityActivityDigestTitle)
        let numbers = digestNumbers(context.params)
        guard let top = digestTop(context) else {
            return NotificationContent(
                title: title,
                body: .message(
                    .notificationsKindCommunityActivityDigestBody,
                    textParameters: texts,
                    numberParameters: numbers
                )
            )
        }
        texts["top"] = top
        return NotificationContent(
            title: title,
            body: .message(
                .notificationsKindCommunityActivityDigestBodyWithTop,
                textParameters: texts,
                numberParameters: numbers
            )
        )
    }

    /// Net is joins minus departures; rate is the rounded percentage of active posters, 0 without members.
    private static func digestNumbers(_ params: NotificationParams) -> [String: Double] {
        let joins = params.number("joins")
        let departures = params.number("departures")
        let total = params.number("total_members")
        let active = params.number("active_posters")
        return [
            "joins": joins,
            "departures": departures,
            "net": joins - departures,
            "total": total,
            "discussions": params.number("discussions"),
            "reviews": params.number("reviews"),
            "dataPoints": params.number("data_points"),
            "comments": params.number("comments"),
            "active": active,
            "rate": total == 0 ? 0 : (active * 100 / total).rounded(),
            "moderation": params.number("moderation_workload")
        ]
    }

    private static func digestCommunities(_ params: NotificationParams) -> UiVerbatimText {
        let count = params.number("community_count")
        return .message(
            count == 1
                ? .notificationsKindCommunityActivityDigestCommunitiesOne
                : .notificationsKindCommunityActivityDigestCommunitiesOther,
            numberParameters: ["count": count]
        )
    }

    /// The top discussion fragment, present only when its post and that post's community are hydrated.
    private static func digestTop(_ context: NotificationTextContext) -> UiVerbatimText? {
        guard let top = context.notification.topPostId.flatMap({ context.hydration.posts[$0] }),
              let community = top.communityId.flatMap({ context.hydration.communities[$0] })
        else { return nil }
        return .message(
            .notificationsKindCommunityActivityDigestTop,
            textParameters: ["community": .userContent(community.name), "title": .userContent(top.snippet)],
            numberParameters: ["replies": context.params.number("top_discussion_replies")]
        )
    }
}
