import Foundation

public struct PrivateUser: Codable, Identifiable, Sendable {
    public let entityType: String
    public let id: String
    public let username: String?
    public let useDisplayNameFrom: DisplayNameSource?
    public let roles: [String]
    public let accountType: AccountType?
    public let profileImageId: String?
    public let profileImagePlacement: ImagePlacement?
    public let markdown: String?
    public let emailAddress: String?
    public let membershipPlan: String?
    public let verificationStatus: String?
    public let suspendedAt: Date?
    public let cardsVisibility: UserPrivacyAudience
    public let rewardsProgramStatusesVisibility: UserPrivacyAudience
    public let spendingCategoriesVisibility: UserPrivacyAudience
    public let followsVisibility: UserPrivacyAudience
    public let topicFollowsVisibility: UserPrivacyAudience
    public let rssFeedFollowsVisibility: UserPrivacyAudience
    public let communityMembershipsVisibility: UserPrivacyAudience
    public let followersVisibility: UserPrivacyAudience
    public let likesVisibility: UserPrivacyAudience
    public let directMessagesAudience: UserPrivacyAudience
    public let defaultPostBroadcast: String
    public let defaultPostPrivacy: String
    public let engagementEmailsEnabled: Bool
    public let newsDigestFrequency: String
    public let moderationEmailsEnabled: Bool
    public let communityDigestFrequency: String
    public let moderationEmailCadence: String
    public let moderationEmailDaysOfWeek: [Int]
    public let moderationEmailTimeOfDay: String
    public let moderationEmailTimezone: String?
    public let fediverseFederationEnabled: Bool?
    public let blueskyAccount: BlueskyAccount?
    public let facebookAccount: OAuthAccountInfo?
    public let xAccount: OAuthAccountInfo?
    public let githubAccount: OAuthAccountInfo?
    public let processingRestrictedAt: Date?
    public let thirdPartyMarketing: Bool?
    public let hnDiscussions: Bool?
    public let country: String?
    public let uiLocale: String?

    enum CodingKeys: String, CodingKey {
        case id, username, useDisplayNameFrom, roles, accountType, profileImageId, profileImagePlacement, markdown,
             emailAddress
        case membershipPlan, verificationStatus, suspendedAt, cardsVisibility
        case rewardsProgramStatusesVisibility, spendingCategoriesVisibility, followsVisibility
        case topicFollowsVisibility, rssFeedFollowsVisibility, communityMembershipsVisibility
        case followersVisibility, likesVisibility, directMessagesAudience, defaultPostBroadcast
        case defaultPostPrivacy, newsDigestFrequency
        case communityDigestFrequency, moderationEmailCadence
        case moderationEmailDaysOfWeek, moderationEmailTimeOfDay, moderationEmailTimezone
        case blueskyAccount, facebookAccount, xAccount, githubAccount
        case processingRestrictedAt, thirdPartyMarketing, hnDiscussions, country, uiLocale
        case engagementEmailsEnabled = "isEngagementEmailsEnabled"
        case moderationEmailsEnabled = "isModerationEmailsEnabled"
        case fediverseFederationEnabled = "isFediverseFederationEnabled"
    }

}
