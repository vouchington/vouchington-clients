import Foundation

extension PrivateUser {
    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        entityType = try Self.decodeEntityType(from: decoder)
        id = try container.decode(String.self, forKey: .id)
        username = try container.decodeIfPresent(String.self, forKey: .username)
        useDisplayNameFrom = try container.decodeIfPresent(DisplayNameSource.self, forKey: .useDisplayNameFrom)
        (roles, isOfficialAccount) = try Self.decodeVotingIdentity(from: container)
        profileImageId = try container.decodeIfPresent(String.self, forKey: .profileImageId)
        markdown = try container.decodeIfPresent(String.self, forKey: .markdown)
        emailAddress = try container.decodeIfPresent(String.self, forKey: .emailAddress)
        membershipPlan = try container.decodeIfPresent(String.self, forKey: .membershipPlan)
        verificationStatus = try container.decodeIfPresent(String.self, forKey: .verificationStatus)
        suspendedAt = try container.decodeIfPresent(Date.self, forKey: .suspendedAt)
        let privacy = try Self.decodePrivacyAudiences(from: container)
        cardsVisibility = privacy.cardsVisibility
        rewardsProgramStatusesVisibility = privacy.rewardsProgramStatusesVisibility
        spendingCategoriesVisibility = privacy.spendingCategoriesVisibility
        followsVisibility = privacy.followsVisibility
        topicFollowsVisibility = privacy.topicFollowsVisibility
        rssFeedFollowsVisibility = privacy.rssFeedFollowsVisibility
        communityMembershipsVisibility = privacy.communityMembershipsVisibility
        followersVisibility = privacy.followersVisibility
        likesVisibility = privacy.likesVisibility
        directMessagesAudience = privacy.directMessagesAudience
        defaultPostBroadcast = try container.decode(String.self, forKey: .defaultPostBroadcast)
        defaultPostPrivacy = try container.decode(String.self, forKey: .defaultPostPrivacy)
        engagementEmailsEnabled = try container.decode(Bool.self, forKey: .engagementEmailsEnabled)
        newsDigestFrequency = try container.decode(String.self, forKey: .newsDigestFrequency)
        moderationEmailsEnabled = try container.decode(Bool.self, forKey: .moderationEmailsEnabled)
        communityDigestFrequency = try container.decode(String.self, forKey: .communityDigestFrequency)
        moderationEmailCadence = try container.decode(String.self, forKey: .moderationEmailCadence)
        moderationEmailDaysOfWeek = try container.decode([Int].self, forKey: .moderationEmailDaysOfWeek)
        moderationEmailTimeOfDay = try container.decode(String.self, forKey: .moderationEmailTimeOfDay)
        moderationEmailTimezone = try container.decodeIfPresent(String.self, forKey: .moderationEmailTimezone)
        fediverseFederationEnabled = try container.decodeIfPresent(Bool.self, forKey: .fediverseFederationEnabled)
        blueskyAccount = try container.decodeIfPresent(BlueskyAccount.self, forKey: .blueskyAccount)
        facebookAccount = try container.decodeIfPresent(OAuthAccountInfo.self, forKey: .facebookAccount)
        xAccount = try container.decodeIfPresent(OAuthAccountInfo.self, forKey: .xAccount)
        githubAccount = try container.decodeIfPresent(OAuthAccountInfo.self, forKey: .githubAccount)
        processingRestrictedAt = try container.decodeIfPresent(Date.self, forKey: .processingRestrictedAt)
        thirdPartyMarketing = try container.decodeIfPresent(Bool.self, forKey: .thirdPartyMarketing)
        hnDiscussions = try container.decodeIfPresent(Bool.self, forKey: .hnDiscussions)
        country = try container.decodeIfPresent(String.self, forKey: .country)
        uiLocale = try container.decodeIfPresent(String.self, forKey: .uiLocale)
    }

    private static func decodeVotingIdentity(
        from container: KeyedDecodingContainer<CodingKeys>
    ) throws -> ([String], Bool) {
        try (
            container.decode([String].self, forKey: .roles),
            container.decode(Bool.self, forKey: .isOfficialAccount)
        )
    }

    private struct DecodedPrivacyAudiences {
        let cardsVisibility: UserPrivacyAudience
        let rewardsProgramStatusesVisibility: UserPrivacyAudience
        let spendingCategoriesVisibility: UserPrivacyAudience
        let followsVisibility: UserPrivacyAudience
        let topicFollowsVisibility: UserPrivacyAudience
        let rssFeedFollowsVisibility: UserPrivacyAudience
        let communityMembershipsVisibility: UserPrivacyAudience
        let followersVisibility: UserPrivacyAudience
        let likesVisibility: UserPrivacyAudience
        let directMessagesAudience: UserPrivacyAudience
    }

    private static func decodePrivacyAudiences(
        from container: KeyedDecodingContainer<CodingKeys>
    ) throws -> DecodedPrivacyAudiences {
        try DecodedPrivacyAudiences(
            cardsVisibility: container.decode(UserPrivacyAudience.self, forKey: .cardsVisibility),
            rewardsProgramStatusesVisibility: container.decode(
                UserPrivacyAudience.self,
                forKey: .rewardsProgramStatusesVisibility
            ),
            spendingCategoriesVisibility: container.decode(
                UserPrivacyAudience.self,
                forKey: .spendingCategoriesVisibility
            ),
            followsVisibility: container.decode(UserPrivacyAudience.self, forKey: .followsVisibility),
            topicFollowsVisibility: container.decode(UserPrivacyAudience.self, forKey: .topicFollowsVisibility),
            rssFeedFollowsVisibility: container.decode(UserPrivacyAudience.self, forKey: .rssFeedFollowsVisibility),
            communityMembershipsVisibility: container.decode(
                UserPrivacyAudience.self,
                forKey: .communityMembershipsVisibility
            ),
            followersVisibility: container.decode(UserPrivacyAudience.self, forKey: .followersVisibility),
            likesVisibility: container.decode(UserPrivacyAudience.self, forKey: .likesVisibility),
            directMessagesAudience: container.decode(UserPrivacyAudience.self, forKey: .directMessagesAudience)
        )
    }

    private static func decodeEntityType(from decoder: any Decoder) throws -> String {
        let raw = try decoder.singleValueContainer().decode([String: DecodedJSONValue].self)
        guard case let .string(entityType) = raw["__entity_type"] else {
            throw DecodingError.keyNotFound(
                CodingKeys.id,
                .init(codingPath: decoder.codingPath, debugDescription: "Missing __entity_type")
            )
        }
        return entityType
    }
}
