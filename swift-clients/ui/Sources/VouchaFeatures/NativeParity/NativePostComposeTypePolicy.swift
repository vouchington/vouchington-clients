import VouchaModels

extension NativePostComposeViewModel {
    var availablePostTypes: [PostType] {
        Self.availablePostTypes(communityIdOrSlug: communityIdOrSlug, isAdministrator: isAdministrator)
    }

    static func normalizedPostType(
        _ postType: PostType,
        communityIdOrSlug: String?,
        isAdministrator: Bool
    ) -> PostType {
        let availableTypes = availablePostTypes(communityIdOrSlug: communityIdOrSlug, isAdministrator: isAdministrator)
        return availableTypes.contains(postType) ? postType : availableTypes[0]
    }

    private static func availablePostTypes(communityIdOrSlug: String?, isAdministrator: Bool) -> [PostType] {
        if communityIdOrSlug?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false {
            return [.discussion, .review, .dataPoint]
        }

        var types: [PostType] = [.discussion, .review, .dataPoint, .link]
        if isAdministrator {
            types.append(contentsOf: [.article, .blogPost])
        }
        return types
    }
}
