import VouchaCore
import VouchaLocalization
import VouchaModels

extension ListsItemFilter {
    var titleKey: UiMessageKey {
        switch self {
        case .all: .nativeSwiftCommonAll
        case .reading: .nativeSwiftPresentationValuesReading
        case .watch: .nativeSwiftPresentationValuesWatch
        case .listen: .nativeSwiftPresentationValuesListen
        }
    }
}

extension ListItemType {
    var titleKey: UiMessageKey {
        switch self {
        case .rssFeedItem: .nativeSwiftPresentationValuesRssFeedItem
        case .post: .nativeSwiftPresentationValuesPost
        }
    }
}

func listItemMediaTypeText(_ mediaType: String?) -> UiVerbatimText {
    guard let mediaType else {
        return .message(.nativeSwiftPresentationValuesItem)
    }
    switch mediaType {
    case "article":
        return .message(.nativeSwiftRouteSurfaceArticle)
    case "audio":
        return .message(.nativeSwiftPresentationValuesAudio)
    case "video":
        return .message(.nativeSwiftPresentationValuesVideo)
    default:
        return .verbatim(mediaType)
    }
}

extension CommunityMemberRole {
    var titleKey: UiMessageKey {
        switch self {
        case .owner: .nativeSwiftPresentationValuesOwner
        case .moderator: .nativeSwiftPresentationValuesModerator
        case .member: .nativeSwiftPresentationValuesMember
        }
    }
}

extension ProfileLinkType {
    var titleKey: UiMessageKey {
        switch self {
        case .url: .nativeSwiftSettingsUrl
        case .twitter: .nativeSwiftPresentationValuesTwitter
        case .facebook: .nativeSwiftPresentationValuesFacebook
        case .instagram: .nativeSwiftPresentationValuesInstagram
        case .github: .nativeSwiftPresentationValuesGithub
        case .linkedin: .nativeSwiftPresentationValuesLinkedin
        case .youtube: .nativeSwiftPresentationValuesYoutube
        case .tiktok: .nativeSwiftPresentationValuesTiktok
        }
    }
}

extension UserPrivacyAudience {
    var titleKey: UiMessageKey {
        switch self {
        case .everyone: .nativeSwiftSettingsEveryone
        case .users: .nativeSwiftSettingsUsers
        case .followers: .nativeSwiftSettingsFollowers
        case .mutualFollowers: .nativeSwiftSettingsMutualFollowers
        case .nobody: .nativeSwiftPresentationValuesNobody
        }
    }
}

extension DisplayNameSource {
    var titleKey: UiMessageKey {
        switch self {
        case .username: .nativeSwiftPresentationValuesUsername
        case .facebook: .nativeSwiftPresentationValuesFacebook
        case .xTwitter: .nativeSwiftPresentationValuesX
        case .apple: .nativeSwiftPresentationValuesApple
        case .google: .nativeSwiftPresentationValuesGoogle
        case .linkedin: .nativeSwiftPresentationValuesLinkedin
        case .microsoft: .nativeSwiftPresentationValuesMicrosoft
        case .github: .nativeSwiftPresentationValuesGithub
        }
    }
}

extension FriendRecommendationProvider {
    var titleKey: UiMessageKey {
        switch self {
        case .facebook: .nativeSwiftPresentationValuesFacebook
        case .x: .nativeSwiftPresentationValuesX
        case .github: .nativeSwiftPresentationValuesGithub
        }
    }
}

extension NativeOAuthProvider {
    var titleKey: UiMessageKey {
        switch self {
        case .facebook: .nativeSwiftPresentationValuesFacebook
        case .x: .nativeSwiftPresentationValuesX
        case .github: .nativeSwiftPresentationValuesGithub
        }
    }
}

extension PostType {
    var titleKey: UiMessageKey {
        switch self {
        case .discussion: .nativeSwiftPresentationValuesDiscussion
        case .review: .nativeSwiftPresentationValuesReview
        case .dataPoint: .nativeSwiftPresentationValuesDataPoint
        case .comment: .nativeSwiftDesignSystemComment
        case .article: .nativeSwiftRouteSurfaceArticle
        case .blogPost: .nativeSwiftPresentationValuesBlogPost
        case .story: .nativeSwiftRouteSurfaceStory
        case .link: .nativeSwiftRouteSurfaceLink
        case .topicRecommendation: .nativeSwiftPresentationValuesTopicRecommendation
        }
    }

    var routeSegment: String {
        switch self {
        case .dataPoint: "data-point"
        case .blogPost: "blog-post"
        case .topicRecommendation: "topic-recommendation"
        default: rawValue
        }
    }
}

func supportThreadStatusText(_ status: SupportThreadStatus) -> UiVerbatimText {
    switch status {
    case .open:
        .message(.nativeSwiftPresentationValuesOpen)
    case .assigned:
        .message(.nativeSwiftPresentationValuesAssigned)
    case .resolved:
        .message(.nativeSwiftPresentationValuesResolved)
    case .closed:
        .message(.nativeSwiftRouteSurfaceClosed)
    }
}

func staffSupportThreadStatusFilterText(_ status: StaffSupportThreadStatusFilter) -> UiVerbatimText {
    switch status {
    case .open:
        .message(.nativeSwiftPresentationValuesOpen)
    case .assigned:
        .message(.nativeSwiftPresentationValuesAssigned)
    case .resolved:
        .message(.nativeSwiftPresentationValuesResolved)
    }
}

func membershipPlanText(_ plan: String) -> UiVerbatimText {
    switch plan {
    case "free":
        .message(.nativeSwiftMembershipFree)
    case "plus":
        .message(.nativeSwiftPresentationValuesPlus)
    case "pro":
        .message(.nativeSwiftPresentationValuesPro)
    default:
        .verbatim(plan)
    }
}
