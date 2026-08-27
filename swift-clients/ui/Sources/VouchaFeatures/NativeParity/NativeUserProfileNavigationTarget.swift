import VouchaModels

enum NativeUserProfileNavigationTarget {
    static func post(_ row: NativeUserProfilePostRow) -> String {
        switch row {
        case let .root(post):
            rootPath(for: post, slug: post.slug)
        case let .comment(comment, root):
            "\(rootPath(for: root, slug: nil))/comment/\(comment.id)"
        }
    }

    private static func rootPath(for post: Post, slug: String?) -> String {
        NativeBookmarkRow.postPath(type: post.postType, id: post.id, slug: slug)
            ?? "/posts"
    }

    static func user(_ user: PublicUser) -> String {
        "/user/\(user.username)"
    }

    static func userAdmin(_ idOrUsername: String) -> String {
        "/user/\(idOrUsername)/admin"
    }

    static func topic(_ topic: Topic) -> String {
        NativeEntityDetailPath.topic(
            id: topic.id,
            slug: topic.slug,
            topicType: topic.topicType
        )
    }

    static func source(_ source: RssFeedSource) -> String {
        NativeEntityDetailPath.source(
            feedId: source.id,
            topicId: source.topic?.id,
            topicSlug: source.topic?.slug
        )
    }

    static func community(_ community: Community) -> String {
        "/communities/\(community.slug)"
    }

    static func routeSegment(_ entityType: String) -> String {
        entityType.replacingOccurrences(of: "_", with: "-")
    }
}
