import Foundation

enum PrivateUserTestFixture {
    static func identityEnvelope(
        id: String = "user-1",
        username: String = "alice",
        emailAddress: String? = "alice@example.com",
        membershipPlan: String? = "pro",
        verificationStatus: String? = "verified",
        roles: [String] = [],
        profileImageId: String? = nil,
        markdown: String? = nil,
        overrides: [String: Any] = [:]
    ) -> Data {
        let identity = identityObject(
            id: id,
            username: username,
            emailAddress: emailAddress,
            membershipPlan: membershipPlan,
            verificationStatus: verificationStatus,
            roles: roles,
            profileImageId: profileImageId,
            markdown: markdown,
            overrides: overrides
        )
        return try! JSONSerialization.data(withJSONObject: ["identity": identity])
    }

    static func userData(
        id: String = "user-1",
        username: String = "alice",
        emailAddress: String? = "alice@example.com",
        membershipPlan: String? = "pro",
        verificationStatus: String? = "verified",
        roles: [String] = [],
        profileImageId: String? = nil,
        markdown: String? = nil,
        overrides: [String: Any] = [:]
    ) -> Data {
        let identity = identityObject(
            id: id,
            username: username,
            emailAddress: emailAddress,
            membershipPlan: membershipPlan,
            verificationStatus: verificationStatus,
            roles: roles,
            profileImageId: profileImageId,
            markdown: markdown,
            overrides: overrides
        )
        return try! JSONSerialization.data(withJSONObject: identity)
    }

    static func userEnvelope(
        id: String = "user-1",
        username: String = "alice",
        emailAddress: String? = "alice@example.com",
        membershipPlan: String? = "pro",
        verificationStatus: String? = "verified",
        roles: [String] = [],
        profileImageId: String? = nil,
        markdown: String? = nil,
        overrides: [String: Any] = [:]
    ) -> Data {
        let user = identityObject(
            id: id,
            username: username,
            emailAddress: emailAddress,
            membershipPlan: membershipPlan,
            verificationStatus: verificationStatus,
            roles: roles,
            profileImageId: profileImageId,
            markdown: markdown,
            overrides: overrides
        )
        return try! JSONSerialization.data(withJSONObject: ["user": user])
    }

    private static func identityObject(
        id: String,
        username: String,
        emailAddress: String?,
        membershipPlan: String?,
        verificationStatus: String?,
        roles: [String],
        profileImageId: String?,
        markdown: String?,
        overrides: [String: Any]
    ) -> [String: Any] {
        let nullable: (String?) -> Any = { $0 ?? NSNull() }
        var identity: [String: Any] = [
            "__entity_type": "user",
            "id": id,
            "username": username,
            "roles": roles,
            "account_type": NSNull(),
            "profile_image_id": nullable(profileImageId),
            "markdown": nullable(markdown),
            "email_address": nullable(emailAddress),
            "membership_plan": nullable(membershipPlan),
            "verification_status": nullable(verificationStatus),
            "suspended_at": NSNull(),
            "cards_visibility": "everyone",
            "rewards_program_statuses_visibility": "everyone",
            "spending_categories_visibility": "everyone",
            "follows_visibility": "everyone",
            "topic_follows_visibility": "everyone",
            "rss_feed_follows_visibility": "everyone",
            "community_memberships_visibility": "everyone",
            "followers_visibility": "everyone",
            "likes_visibility": "everyone",
            "direct_messages_audience": "users",
            "default_post_broadcast": "everyone",
            "default_post_privacy": "public",
            "engagement_emails_enabled": true,
            "news_digest_frequency": "weekly",
            "moderation_emails_enabled": true,
            "community_digest_frequency": "weekly",
            "moderation_email_cadence": "daily",
            "moderation_email_days_of_week": [1, 2, 3, 4, 5],
            "moderation_email_time_of_day": "09:00",
            "moderation_email_timezone": "America/Los_Angeles",
            "processing_restricted_at": NSNull(),
            "third_party_marketing": NSNull(),
            "country": NSNull(),
            "ui_locale": NSNull()
        ]
        for (key, value) in overrides {
            identity[key] = value
        }
        return identity
    }
}
