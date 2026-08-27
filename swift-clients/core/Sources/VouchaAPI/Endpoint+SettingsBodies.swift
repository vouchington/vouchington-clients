import VouchaModels

enum JsonNullableString: Encodable {
    case string(String)
    case null

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case let .string(value):
            try container.encode(value)
        case .null:
            try container.encodeNil()
        }
    }
}

struct UpdateMyIdentityBody: Encodable {
    let username: String?
    let useDisplayNameFrom: DisplayNameSource?
    let profileImageId: JsonNullableString?

    private enum CodingKeys: String, CodingKey {
        case username
        case useDisplayNameFrom = "use_display_name_from"
        case profileImageId = "profile_image_id"
    }
}

struct UpdateUserSettingsBody: Encodable {
    let username: String?
    let useDisplayNameFrom: DisplayNameSource?
    let cardsVisibility: UserPrivacyAudience?
    let rewardsProgramStatusesVisibility: UserPrivacyAudience?
    let spendingCategoriesVisibility: UserPrivacyAudience?
    let followsVisibility: UserPrivacyAudience?
    let topicFollowsVisibility: UserPrivacyAudience?
    let rssFeedFollowsVisibility: UserPrivacyAudience?
    let communityMembershipsVisibility: UserPrivacyAudience?
    let followersVisibility: UserPrivacyAudience?
    let likesVisibility: UserPrivacyAudience?
    let directMessagesAudience: UserPrivacyAudience?
    let defaultPostBroadcast: String?
    let defaultPostPrivacy: String?
    let engagementEmailsEnabled: Bool?
    let newsDigestFrequency: String?
    let moderationEmailsEnabled: Bool?
    let communityDigestFrequency: String?
    let moderationEmailCadence: String?
    let moderationEmailDaysOfWeek: [Int]?
    let moderationEmailTimeOfDay: String?
    let moderationEmailTimezone: String?
    let processingRestrictedAt: Bool?
    let thirdPartyMarketing: Bool?
    let hnDiscussions: Bool?
    let country: String?
    let uiLocale: JsonNullableString?

    private enum CodingKeys: String, CodingKey {
        case username
        case useDisplayNameFrom = "use_display_name_from"
        case cardsVisibility = "cards_visibility"
        case rewardsProgramStatusesVisibility = "rewards_program_statuses_visibility"
        case spendingCategoriesVisibility = "spending_categories_visibility"
        case followsVisibility = "follows_visibility"
        case topicFollowsVisibility = "topic_follows_visibility"
        case rssFeedFollowsVisibility = "rss_feed_follows_visibility"
        case communityMembershipsVisibility = "community_memberships_visibility"
        case followersVisibility = "followers_visibility"
        case likesVisibility = "likes_visibility"
        case directMessagesAudience = "direct_messages_audience"
        case defaultPostBroadcast = "default_post_broadcast"
        case defaultPostPrivacy = "default_post_privacy"
        case engagementEmailsEnabled = "engagement_emails_enabled"
        case newsDigestFrequency = "news_digest_frequency"
        case moderationEmailsEnabled = "moderation_emails_enabled"
        case communityDigestFrequency = "community_digest_frequency"
        case moderationEmailCadence = "moderation_email_cadence"
        case moderationEmailDaysOfWeek = "moderation_email_days_of_week"
        case moderationEmailTimeOfDay = "moderation_email_time_of_day"
        case moderationEmailTimezone = "moderation_email_timezone"
        case processingRestrictedAt = "processing_restricted_at"
        case thirdPartyMarketing = "third_party_marketing"
        case hnDiscussions = "hn_discussions"
        case country
        case uiLocale = "ui_locale"
    }
}

struct UpdateEmailPreferencesBody: Encodable {
    let engagementEmailsEnabled: Bool?
    let newsDigestFrequency: String?
    let moderationEmailsEnabled: Bool?
    let communityDigestFrequency: String?
    let moderationEmailCadence: String?
    let moderationEmailDaysOfWeek: [Int]?
    let moderationEmailTimeOfDay: String?
    let moderationEmailTimezone: String?

    private enum CodingKeys: String, CodingKey {
        case engagementEmailsEnabled = "engagement_emails_enabled"
        case newsDigestFrequency = "news_digest_frequency"
        case moderationEmailsEnabled = "moderation_emails_enabled"
        case communityDigestFrequency = "community_digest_frequency"
        case moderationEmailCadence = "moderation_email_cadence"
        case moderationEmailDaysOfWeek = "moderation_email_days_of_week"
        case moderationEmailTimeOfDay = "moderation_email_time_of_day"
        case moderationEmailTimezone = "moderation_email_timezone"
    }
}

struct CreateProfileLinkBody: Encodable {
    let linkType: ProfileLinkType
    let url: String?
    let handle: String?
    let name: String?
    let imageId: String?

    private enum CodingKeys: String, CodingKey {
        case linkType = "link_type"
        case url
        case handle
        case name
        case imageId = "image_id"
    }
}

struct UpdateProfileLinkBody: Encodable {
    let url: String?
    let handle: String?
    let name: String?
    let imageId: String?

    private enum CodingKeys: String, CodingKey {
        case url
        case handle
        case name
        case imageId = "image_id"
    }
}

struct ReorderProfileLinksBody: Encodable {
    let ids: [String]
}
