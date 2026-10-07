import Foundation

public extension PrivateUser {
    func encode(to encoder: any Encoder) throws {
        var object = encodedRequiredProperties
        addOptionalProperties(to: &object)
        var container = encoder.singleValueContainer()
        try container.encode(object)
    }

    private var encodedRequiredProperties: [String: DecodedJSONValue] {
        [
            "__entity_type": .string(entityType),
            "id": .string(id),
            "roles": .array(roles.map { .string($0) }),
            "account_type": accountType.map { .string($0.rawValue) } ?? .null,
            "cards_visibility": .string(cardsVisibility.rawValue),
            "rewards_program_statuses_visibility": .string(rewardsProgramStatusesVisibility.rawValue),
            "spending_categories_visibility": .string(spendingCategoriesVisibility.rawValue),
            "follows_visibility": .string(followsVisibility.rawValue),
            "topic_follows_visibility": .string(topicFollowsVisibility.rawValue),
            "rss_feed_follows_visibility": .string(rssFeedFollowsVisibility.rawValue),
            "community_memberships_visibility": .string(communityMembershipsVisibility.rawValue),
            "followers_visibility": .string(followersVisibility.rawValue),
            "likes_visibility": .string(likesVisibility.rawValue),
            "direct_messages_audience": .string(directMessagesAudience.rawValue),
            "default_post_broadcast": .string(defaultPostBroadcast),
            "default_post_privacy": .string(defaultPostPrivacy),
            "engagement_emails_enabled": .bool(engagementEmailsEnabled),
            "news_digest_frequency": .string(newsDigestFrequency),
            "moderation_emails_enabled": .bool(moderationEmailsEnabled),
            "community_digest_frequency": .string(communityDigestFrequency),
            "moderation_email_cadence": .string(moderationEmailCadence),
            "moderation_email_days_of_week": .array(moderationEmailDaysOfWeek.map { .number(Double($0)) }),
            "moderation_email_time_of_day": .string(moderationEmailTimeOfDay)
        ]
    }

    private func addOptionalProperties(to object: inout [String: DecodedJSONValue]) {
        object["username"] = username.map { .string($0) }
        object["use_display_name_from"] = useDisplayNameFrom.map { .string($0.rawValue) }
        object["profile_image_id"] = profileImageId.map { .string($0) }
        object["markdown"] = markdown.map { .string($0) }
        object["email_address"] = emailAddress.map { .string($0) }
        object["membership_plan"] = membershipPlan.map { .string($0) } ?? .null
        object["verification_status"] = verificationStatus.map { .string($0) }
        object["suspended_at"] = suspendedAt.map { .string(ISO8601DateFormatter().string(from: $0)) }
        object["moderation_email_timezone"] = moderationEmailTimezone.map { .string($0) }
        object["fediverse_federation_enabled"] = fediverseFederationEnabled.map { .bool($0) }
        object["bluesky_account"] = blueskyAccount.map {
            .object([
                "did": .string($0.did),
                "handle": $0.handle.map { .string($0) } ?? .null
            ])
        }
        object["facebook_account"] = facebookAccount.map { Self.encodedOAuthAccount($0) }
        object["x_account"] = xAccount.map { Self.encodedOAuthAccount($0) }
        object["github_account"] = githubAccount.map { Self.encodedOAuthAccount($0) }
        object["processing_restricted_at"] = processingRestrictedAt
            .map { .string(ISO8601DateFormatter().string(from: $0)) }
        object["third_party_marketing"] = thirdPartyMarketing.map { .bool($0) }
        object["hn_discussions"] = hnDiscussions.map { .bool($0) }
        object["country"] = country.map { .string($0) }
        object["ui_locale"] = uiLocale.map { .string($0) }
    }

    private static func encodedOAuthAccount(_ account: OAuthAccountInfo) -> DecodedJSONValue {
        .object([
            "id": .string(account.id),
            "name": account.name.map { .string($0) } ?? .null,
            "email_address": account.emailAddress.map { .string($0) } ?? .null
        ])
    }
}
