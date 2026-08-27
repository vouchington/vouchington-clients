import VouchaLocalization
import VouchaModels

extension ListVisibility {
    var titleKey: UiMessageKey {
        switch self {
        case .private: .nativeTaxonomyListsPrivate
        case .unlisted: .nativeTaxonomyListsUnlisted
        case .public: .nativeTaxonomyListsPublic
        }
    }
}

extension PostType {
    var protocolValue: String {
        rawValue
    }
}

extension CommunityVisibility {
    var titleKey: UiMessageKey {
        switch self {
        case .public: .nativeTaxonomyListsPublic
        case .private: .nativeTaxonomyListsPrivate
        }
    }
}

extension CommunityMemberRosterVisibility {
    var titleKey: UiMessageKey {
        switch self {
        case .public: .nativeTaxonomyListsPublic
        case .users: .nativeTaxonomyCommunityUsers
        case .members: .nativeTaxonomyCommunityMembers
        case .moderators: .nativeTaxonomyCommunityModerators
        }
    }
}

extension CommunityListType {
    var titleKey: UiMessageKey {
        switch self {
        case .follow: .nativeTaxonomyCommunityFollow
        case .mute: .nativeTaxonomyCommunityMute
        }
    }
}

extension CommunityRestrictionType {
    var titleKey: UiMessageKey {
        switch self {
        case .requirePostApproval: .nativeTaxonomyCommunityRequirePostApproval
        case .noNewMemberPosts: .nativeTaxonomyCommunityNoNewMemberPosts
        case .noLinks: .nativeTaxonomyCommunityNoLinks
        case .approvedMembersOnly: .nativeTaxonomyCommunityApprovedMembersOnly
        }
    }
}

extension CommunityAutomodActionCurrentState {
    var titleKey: UiMessageKey {
        switch self {
        case .rejected: .nativeTaxonomyModerationRejected
        case .inReview: .nativeTaxonomyModerationInReview
        case .unpublished: .nativeTaxonomyModerationUnpublished
        }
    }
}

extension DataPointVertical {
    var titleKey: UiMessageKey {
        switch self {
        case .creditCard: .nativeTaxonomyDataPointCreditCard
        case .bankAccount: .nativeTaxonomyDataPointBankAccount
        }
    }
}

extension DataRequestStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeTaxonomyModerationPending
        case .processing: .nativeTaxonomyDataRequestProcessing
        case .ready: .nativeTaxonomyDataRequestReady
        case .failed: .nativeTaxonomyDataRequestFailed
        case .expired: .nativeTaxonomyDataRequestExpired
        }
    }
}

extension ModerationAppealStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeTaxonomyModerationPending
        case .dismissed: .nativeTaxonomyModerationDismissed
        case .resolved: .nativeTaxonomyModerationResolved
        }
    }
}

extension ModerationDisputeStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeTaxonomyModerationPending
        case .dismissed: .nativeTaxonomyModerationDismissed
        case .resolved: .nativeTaxonomyModerationResolved
        }
    }
}

extension ReviewDisputeStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeTaxonomyModerationPending
        case .resolved: .nativeTaxonomyModerationResolved
        case .dismissed: .nativeTaxonomyModerationDismissed
        }
    }
}

extension ReviewDisputeAction {
    var titleKey: UiMessageKey {
        switch self {
        case .noAction: .nativeTaxonomyModerationNoAction
        case .remove: .nativeTaxonomyModerationRemove
        case .annotate: .nativeTaxonomyModerationAnnotate
        case .dismiss: .nativeTaxonomyModerationDismissed
        }
    }
}

extension AdminReviewQueueClearanceStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .rejected: .nativeTaxonomyModerationRejected
        case .inReview: .nativeTaxonomyModerationInReview
        case .approved: .nativeTaxonomyModerationApproved
        case .pending: .nativeTaxonomyModerationPending
        }
    }
}

func communityMemberRoleText(_ role: String) -> UiVerbatimText {
    switch role {
    case "owner": .message(.nativeSwiftPresentationValuesOwner)
    case "moderator": .message(.nativeSwiftPresentationValuesModerator)
    case "member": .message(.nativeSwiftPresentationValuesMember)
    default: .protocolValue(role)
    }
}

func postTypeText(_ postType: PostType) -> UiVerbatimText {
    .message(postType.titleKey)
}

func postTypeText(_ postType: String) -> UiVerbatimText {
    PostType(rawValue: postType).map { postTypeText($0) } ?? .protocolValue(postType)
}

func listVisibilityText(_ visibility: ListVisibility) -> UiVerbatimText {
    .message(visibility.titleKey)
}
