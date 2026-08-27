import Foundation
import VouchaModels

public struct CommunityApplicationQuestionInput: Encodable, Sendable {
    public let question: String
    public let fieldType: CommunityApplicationQuestionFieldType
    public let options: [String]?
    public let required: Bool?

    public init(
        question: String,
        fieldType: CommunityApplicationQuestionFieldType,
        options: [String]? = nil,
        required: Bool? = nil
    ) {
        self.question = question
        self.fieldType = fieldType
        self.options = options
        self.required = required
    }
}

public extension Endpoint {
    static func communities(
        query: String? = nil,
        after: String? = nil,
        limit: Int = 20,
        sort: String? = nil,
        memberId: String? = nil,
        listScope: String? = nil,
        feedCategory: String? = nil,
        listType: CommunityListType? = nil,
        hasListType: Bool? = nil,
        hasListItems: Bool? = nil,
        eligiblePostType: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let query {
            items.append(.init(name: "q", value: query))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let sort {
            items.append(.init(name: "sort", value: sort))
        }
        if let memberId {
            items.append(.init(name: "member_id", value: memberId))
        }
        if let listScope {
            items.append(.init(name: "list_scope", value: listScope))
        }
        if let feedCategory {
            items.append(.init(name: "feed_category", value: feedCategory))
        }
        if let listType {
            items.append(.init(name: "list_type", value: listType.rawValue))
        }
        if let hasListType {
            items.append(.init(name: "has_list_type", value: hasListType ? "true" : "false"))
        }
        if let hasListItems {
            items.append(.init(name: "has_list_items", value: hasListItems ? "true" : "false"))
        }
        if let eligiblePostType {
            items.append(.init(name: "eligible_post_type", value: eligiblePostType))
        }
        return Endpoint(.GET, path: "/api/v1/communities", queryItems: items)
    }

    static func community(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))")
    }

    static func createCommunity(
        name: String,
        slug: String? = nil,
        markdown: String? = nil,
        visibility: CommunityVisibility? = nil,
        listType: CommunityListType? = nil,
        memberRosterVisibility: CommunityMemberRosterVisibility? = nil,
        memberInvitesAllowedAt: Bool? = nil,
        postApprovalRequiredAt: Bool? = nil,
        allowReviewPosts: Bool? = nil,
        allowDataPointPosts: Bool? = nil,
        profileImageId: String? = nil,
        bannerImageId: String? = nil,
        defaultLanguage: String? = nil,
        turnstileToken: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities",
            body: CommunityCreateBody(
                name: name,
                slug: slug,
                markdown: markdown,
                visibility: visibility,
                listType: listType,
                memberRosterVisibility: memberRosterVisibility,
                memberInvitesAllowedAt: memberInvitesAllowedAt,
                postApprovalRequiredAt: postApprovalRequiredAt,
                allowReviewPosts: allowReviewPosts,
                allowDataPointPosts: allowDataPointPosts,
                profileImageId: profileImageId,
                bannerImageId: bannerImageId,
                defaultLanguage: defaultLanguage,
                cfTurnstileResponse: turnstileToken
            )
        )
    }

    static func updateCommunity(
        idOrSlug: String,
        name: String? = nil,
        slug: String? = nil,
        markdown: String? = nil,
        clearMarkdown: Bool = false,
        visibility: CommunityVisibility? = nil,
        memberRosterVisibility: CommunityMemberRosterVisibility? = nil,
        listType: CommunityListType? = nil,
        clearListType: Bool = false,
        memberInvitesAllowedAt: Bool? = nil,
        postApprovalRequiredAt: Bool? = nil,
        profileImageId: String? = nil,
        clearProfileImageId: Bool = false,
        bannerImageId: String? = nil,
        clearBannerImageId: Bool = false,
        defaultLanguage: String? = nil,
        clearDefaultLanguage: Bool = false,
        archive: Bool? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))",
            body: CommunityUpdateBody(
                name: name,
                slug: slug,
                markdown: markdown,
                clearMarkdown: clearMarkdown,
                visibility: visibility,
                memberRosterVisibility: memberRosterVisibility,
                listType: listType,
                clearListType: clearListType,
                memberInvitesAllowedAt: memberInvitesAllowedAt,
                postApprovalRequiredAt: postApprovalRequiredAt,
                profileImageId: profileImageId,
                clearProfileImageId: clearProfileImageId,
                bannerImageId: bannerImageId,
                clearBannerImageId: clearBannerImageId,
                defaultLanguage: defaultLanguage,
                clearDefaultLanguage: clearDefaultLanguage,
                archive: archive
            )
        )
    }

    static func deleteCommunity(idOrSlug: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))")
    }

    static func joinCommunity(idOrSlug: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/communities/\(pathSegment(idOrSlug))/members")
    }

    static func leaveCommunity(idOrSlug: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/members")
    }

    static func archiveCommunity(idOrSlug: String) -> Endpoint {
        updateCommunity(idOrSlug: idOrSlug, archive: true)
    }

    static func unarchiveCommunity(idOrSlug: String) -> Endpoint {
        updateCommunity(idOrSlug: idOrSlug, archive: false)
    }
}
