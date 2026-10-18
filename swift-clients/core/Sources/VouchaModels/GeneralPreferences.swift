import Foundation

/// The twelve settings exposed by the general member-preferences tool option.
public struct GeneralPreferences: Codable, Equatable, Sendable {
    public let followsVisibility: UserPrivacyAudience
    public let topicFollowsVisibility: UserPrivacyAudience
    public let rssFeedFollowsVisibility: UserPrivacyAudience
    public let communityMembershipsVisibility: UserPrivacyAudience
    public let followersVisibility: UserPrivacyAudience
    public let likesVisibility: UserPrivacyAudience
    public let directMessagesAudience: UserPrivacyAudience
    public let defaultPostBroadcast: BroadcastScope
    public let defaultPostPrivacy: PostPrivacy
    public let country: String?
    public let uiLocale: String?
    public let shouldImportHackerNewsDiscussions: Bool

    private enum CodingKeys: String, CodingKey {
        case followsVisibility, topicFollowsVisibility, rssFeedFollowsVisibility, communityMembershipsVisibility
        case followersVisibility, likesVisibility, directMessagesAudience, defaultPostBroadcast, defaultPostPrivacy
        case country, uiLocale, shouldImportHackerNewsDiscussions
    }

    public init(from decoder: any Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        followsVisibility = try values.decode(UserPrivacyAudience.self, forKey: .followsVisibility)
        topicFollowsVisibility = try values.decode(UserPrivacyAudience.self, forKey: .topicFollowsVisibility)
        rssFeedFollowsVisibility = try values.decode(UserPrivacyAudience.self, forKey: .rssFeedFollowsVisibility)
        communityMembershipsVisibility = try values.decode(
            UserPrivacyAudience.self,
            forKey: .communityMembershipsVisibility
        )
        followersVisibility = try values.decode(UserPrivacyAudience.self, forKey: .followersVisibility)
        likesVisibility = try values.decode(UserPrivacyAudience.self, forKey: .likesVisibility)
        directMessagesAudience = try values.decode(UserPrivacyAudience.self, forKey: .directMessagesAudience)
        defaultPostBroadcast = try values.decode(BroadcastScope.self, forKey: .defaultPostBroadcast)
        defaultPostPrivacy = try values.decode(PostPrivacy.self, forKey: .defaultPostPrivacy)
        // Both keys are required even when the account deliberately uses the default.
        country = try values.decode(String?.self, forKey: .country)
        uiLocale = try values.decode(String?.self, forKey: .uiLocale)
        shouldImportHackerNewsDiscussions = try values.decode(Bool.self, forKey: .shouldImportHackerNewsDiscussions)
    }

    public func encode(to encoder: any Encoder) throws {
        var values = encoder.container(keyedBy: CodingKeys.self)
        try values.encode(followsVisibility, forKey: .followsVisibility)
        try values.encode(topicFollowsVisibility, forKey: .topicFollowsVisibility)
        try values.encode(rssFeedFollowsVisibility, forKey: .rssFeedFollowsVisibility)
        try values.encode(communityMembershipsVisibility, forKey: .communityMembershipsVisibility)
        try values.encode(followersVisibility, forKey: .followersVisibility)
        try values.encode(likesVisibility, forKey: .likesVisibility)
        try values.encode(directMessagesAudience, forKey: .directMessagesAudience)
        try values.encode(defaultPostBroadcast, forKey: .defaultPostBroadcast)
        try values.encode(defaultPostPrivacy, forKey: .defaultPostPrivacy)
        try values.encode(country, forKey: .country)
        try values.encode(uiLocale, forKey: .uiLocale)
        try values.encode(shouldImportHackerNewsDiscussions, forKey: .shouldImportHackerNewsDiscussions)
    }
}
