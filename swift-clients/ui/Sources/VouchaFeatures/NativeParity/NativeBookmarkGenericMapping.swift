import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func genericBookmarkTitle(_ entity: NativeGenericEntity, entityType: String) -> UiVerbatimText {
        let title = entity.title ?? entity.name ?? entity.subject ?? entity.username ?? entity.hostname?.displayName
            ?? entity.url ?? entity.slug
        guard let title, title != entity.id else {
            return .message(friendlyBookmarkEntityName(entityType))
        }
        return .userContent(title)
    }

    func genericBookmarkDetail(_ entity: NativeGenericEntity, entityType: String) -> UiVerbatimText {
        if let detail = entity.description ?? entity.summary ?? entity.status ?? entity.topicType ?? entity.postType
            ?? entity.feedType ?? entity.pathname ?? entity.slug {
            return .userContent(detail)
        }
        return .message(friendlyBookmarkEntityName(entityType))
    }

    func friendlyBookmarkEntityName(_ entityType: String) -> UiMessageKey {
        switch entityType {
        case "topic": .nativeSwiftHouseholdsBookmarksTopic
        case "user": .nativeSwiftHouseholdsBookmarksUser
        case "hostname": .nativeSwiftHouseholdsBookmarksDomain
        case "url": .nativeSwiftHouseholdsBookmarksUrl
        case "community": .nativeSwiftHouseholdsBookmarksCommunity
        default: .nativeSwiftHouseholdsBookmarksBookmark
        }
    }

    func genericBookmarkPath(_ entity: NativeGenericEntity, entityType: String) -> String {
        switch entityType {
        case "topic":
            NativeEntityDetailPath.topic(
                id: entity.id,
                slug: entity.slug,
                topicType: entity.topicType
            )
        case "user": "/user/\(entity.username ?? entity.id)"
        case "hostname": "/domain/\(entity.hostname?.displayName ?? entity.id)"
        case "url": "/url/\(entity.id)"
        case "community": "/communities/\(entity.slug ?? entity.id)"
        default: entity.pathname ?? "/"
        }
    }

    func genericBookmarkIcon(
        _ entity: NativeGenericEntity,
        response: NativeGenericListResponse,
        fallback: String
    ) -> String {
        if response.topics?[entity.id] != nil {
            return "tag"
        }
        if response.communities?[entity.id] != nil {
            return "person.3"
        }
        if response.users?[entity.id] != nil {
            return "person"
        }
        if response.hostnames?[entity.id] != nil {
            return "globe"
        }
        if response.supportThreads?[entity.id] != nil {
            return "questionmark.bubble"
        }
        return fallback
    }
}
