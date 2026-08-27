import VouchaModels

public extension Endpoint {
    static var myEmailPreferences: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/email-preferences")
    }

    static func updateMyEmailPreferences(
        engagementEmailsEnabled: Bool? = nil,
        newsDigestFrequency: String? = nil,
        moderationEmailsEnabled: Bool? = nil,
        communityDigestFrequency: String? = nil,
        moderationEmailCadence: String? = nil,
        moderationEmailDaysOfWeek: [Int]? = nil,
        moderationEmailTimeOfDay: String? = nil,
        moderationEmailTimezone: String? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/email-preferences",
            body: UpdateEmailPreferencesBody(
                engagementEmailsEnabled: engagementEmailsEnabled,
                newsDigestFrequency: newsDigestFrequency,
                moderationEmailsEnabled: moderationEmailsEnabled,
                communityDigestFrequency: communityDigestFrequency,
                moderationEmailCadence: moderationEmailCadence,
                moderationEmailDaysOfWeek: moderationEmailDaysOfWeek,
                moderationEmailTimeOfDay: moderationEmailTimeOfDay,
                moderationEmailTimezone: moderationEmailTimezone
            )
        )
    }

    static func updateMyIdentity(
        username: String? = nil,
        useDisplayNameFrom: DisplayNameSource? = nil,
        profileImageId: String? = nil,
        clearProfileImage: Bool = false
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/identity",
            body: UpdateMyIdentityBody(
                username: username,
                useDisplayNameFrom: useDisplayNameFrom,
                profileImageId: clearProfileImage ? .null : profileImageId.map(JsonNullableString.string)
            )
        )
    }

    static var myProfileLinks: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/profile/links")
    }

    static func createMyProfileLink(
        linkType: ProfileLinkType,
        url: String? = nil,
        handle: String? = nil,
        name: String? = nil,
        imageId: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/profile/links",
            body: CreateProfileLinkBody(linkType: linkType, url: url, handle: handle, name: name, imageId: imageId)
        )
    }

    static func reorderMyProfileLinks(ids: [String]) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/my/profile/links/order", body: ReorderProfileLinksBody(ids: ids))
    }

    static func updateMyProfileLink(
        id: String,
        url: String? = nil,
        handle: String? = nil,
        name: String? = nil,
        imageId: String? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/profile/links/\(pathSegment(id))",
            body: UpdateProfileLinkBody(url: url, handle: handle, name: name, imageId: imageId)
        )
    }

    static func deleteMyProfileLink(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/profile/links/\(pathSegment(id))")
    }

    static func updateUser(
        idOrSlug: String,
        username: String? = nil,
        useDisplayNameFrom: DisplayNameSource? = nil,
        cardsVisibility: UserPrivacyAudience? = nil,
        rewardsProgramStatusesVisibility: UserPrivacyAudience? = nil,
        spendingCategoriesVisibility: UserPrivacyAudience? = nil,
        followsVisibility: UserPrivacyAudience? = nil,
        topicFollowsVisibility: UserPrivacyAudience? = nil,
        rssFeedFollowsVisibility: UserPrivacyAudience? = nil,
        communityMembershipsVisibility: UserPrivacyAudience? = nil,
        followersVisibility: UserPrivacyAudience? = nil,
        likesVisibility: UserPrivacyAudience? = nil,
        directMessagesAudience: UserPrivacyAudience? = nil,
        defaultPostBroadcast: String? = nil,
        defaultPostPrivacy: String? = nil,
        engagementEmailsEnabled: Bool? = nil,
        newsDigestFrequency: String? = nil,
        moderationEmailsEnabled: Bool? = nil,
        communityDigestFrequency: String? = nil,
        moderationEmailCadence: String? = nil,
        moderationEmailDaysOfWeek: [Int]? = nil,
        moderationEmailTimeOfDay: String? = nil,
        moderationEmailTimezone: String? = nil,
        processingRestrictedAt: Bool? = nil,
        thirdPartyMarketing: Bool? = nil,
        hnDiscussions: Bool? = nil,
        country: String? = nil,
        uiLocale: String? = nil,
        clearUiLocale: Bool = false
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/users/\(pathSegment(idOrSlug))",
            body: UpdateUserSettingsBody(
                username: username,
                useDisplayNameFrom: useDisplayNameFrom,
                cardsVisibility: cardsVisibility,
                rewardsProgramStatusesVisibility: rewardsProgramStatusesVisibility,
                spendingCategoriesVisibility: spendingCategoriesVisibility,
                followsVisibility: followsVisibility,
                topicFollowsVisibility: topicFollowsVisibility,
                rssFeedFollowsVisibility: rssFeedFollowsVisibility,
                communityMembershipsVisibility: communityMembershipsVisibility,
                followersVisibility: followersVisibility,
                likesVisibility: likesVisibility,
                directMessagesAudience: directMessagesAudience,
                defaultPostBroadcast: defaultPostBroadcast,
                defaultPostPrivacy: defaultPostPrivacy,
                engagementEmailsEnabled: engagementEmailsEnabled,
                newsDigestFrequency: newsDigestFrequency,
                moderationEmailsEnabled: moderationEmailsEnabled,
                communityDigestFrequency: communityDigestFrequency,
                moderationEmailCadence: moderationEmailCadence,
                moderationEmailDaysOfWeek: moderationEmailDaysOfWeek,
                moderationEmailTimeOfDay: moderationEmailTimeOfDay,
                moderationEmailTimezone: moderationEmailTimezone,
                processingRestrictedAt: processingRestrictedAt,
                thirdPartyMarketing: thirdPartyMarketing,
                hnDiscussions: hnDiscussions,
                country: country,
                uiLocale: clearUiLocale ? .null : uiLocale.map(JsonNullableString.string)
            )
        )
    }

    static func deleteUser(idOrSlug: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/users/\(pathSegment(idOrSlug))")
    }

    static func userDataRequest(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/users/\(pathSegment(idOrSlug))/data-request")
    }

    static func createUserDataRequest(idOrSlug: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/users/\(pathSegment(idOrSlug))/data-request")
    }
}
