import Foundation
import VouchaModels

extension SettingsViewModel {
    func apply(identity: PrivateUser) {
        if let previousOwnerId = apiKeyRotationOwnerState.lastConfirmedOwnerId,
           previousOwnerId != identity.id {
            invalidateApiKeyRotationOwner()
        }
        if self.identity?.roles.contains("administrator") != identity.roles.contains("administrator") {
            apiKeyLifetimeDays = identity.roles.contains("administrator") ? 30 : 90
        }
        self.identity = identity
        confirmApiKeyRotationOwner(identity.id)
        username = identity.username ?? ""
        displayNameSource = identity.useDisplayNameFrom ?? .username
        profileImageId = identity.profileImageId ?? ""
        followsVisibility = identity.followsVisibility
        topicFollowsVisibility = identity.topicFollowsVisibility
        rssFeedFollowsVisibility = identity.rssFeedFollowsVisibility
        communityMembershipsVisibility = identity.communityMembershipsVisibility
        followersVisibility = identity.followersVisibility
        likesVisibility = identity.likesVisibility
        cardsVisibility = identity.cardsVisibility
        rewardsProgramStatusesVisibility = identity.rewardsProgramStatusesVisibility
        spendingCategoriesVisibility = identity.spendingCategoriesVisibility
        directMessagesAudience = identity.directMessagesAudience
        savedUiLocale = identity.uiLocale
        uiLocale = identity.uiLocale ?? Self.siteDefaultUiLocale
        uiLocaleController?.update(savedUiLocale: identity.uiLocale)
        defaultPostBroadcast = identity.defaultPostBroadcast
        defaultPostPrivacy = identity.defaultPostPrivacy
        processingRestrictedAt = identity.processingRestrictedAt != nil
        thirdPartyMarketing = identity.thirdPartyMarketing ?? false
        hnDiscussions = identity.hnDiscussions ?? false
    }
}
