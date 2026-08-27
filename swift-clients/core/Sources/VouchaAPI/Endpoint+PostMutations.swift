import VouchaModels

public struct UpdatePostBody: Encodable, Sendable {
    public let title: String?
    public let markdown: String?
    public let broadcast: BroadcastScope?
    public let privacy: PostPrivacy?
    public let isAnonymous: Bool?
    public let dataPointVertical: DataPointVertical?
    public let structuredData: CreatePostJSONValue?
    public let archive: Bool?

    public init(
        title: String? = nil,
        markdown: String? = nil,
        broadcast: BroadcastScope? = nil,
        privacy: PostPrivacy? = nil,
        isAnonymous: Bool? = nil,
        dataPointVertical: DataPointVertical? = nil,
        structuredData: CreatePostJSONValue? = nil,
        archive: Bool? = nil
    ) {
        self.title = title
        self.markdown = markdown
        self.broadcast = broadcast
        self.privacy = privacy
        self.isAnonymous = isAnonymous
        self.dataPointVertical = dataPointVertical
        self.structuredData = structuredData
        self.archive = archive
    }
}

private struct PostRatingBody: Encodable {
    let topicId: String
    let rating: Int
    let orderIndex: Int?
}

private struct UpdatePostRatingBody: Encodable {
    let rating: Int?
    let orderIndex: Int?
}

private struct SetPostImagesBody: Encodable {
    let images: [CreatePostImageInput]
}

public extension Endpoint {
    static func sharePostWithFollowers(postId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/posts/\(pathSegment(postId))/shares")
    }

    static func sendPostToFollowers(postId: String, request: FollowerDistributionRequest) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/posts/\(pathSegment(postId))/sends", body: request)
    }

    static func updatePost(postId: String, body: UpdatePostBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/posts/\(pathSegment(postId))", body: body)
    }

    static func deletePost(postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/posts/\(pathSegment(postId))")
    }

    static func lockPost(postId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/posts/\(pathSegment(postId))/lock")
    }

    static func unlockPost(postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/posts/\(pathSegment(postId))/lock")
    }

    static func archivePost(postId: String) -> Endpoint {
        updatePost(postId: postId, body: UpdatePostBody(archive: true))
    }

    static func unarchivePost(postId: String) -> Endpoint {
        updatePost(postId: postId, body: UpdatePostBody(archive: false))
    }

    static func addPostRating(postId: String, topicId: String, rating: Int, orderIndex: Int) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts/\(pathSegment(postId))/ratings",
            body: PostRatingBody(topicId: topicId, rating: rating, orderIndex: orderIndex)
        )
    }

    static func updatePostRating(
        postId: String,
        topicId: String,
        rating: Int? = nil,
        orderIndex: Int? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/posts/\(pathSegment(postId))/ratings/\(pathSegment(topicId))",
            body: UpdatePostRatingBody(rating: rating, orderIndex: orderIndex)
        )
    }

    static func deletePostRating(postId: String, topicId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/posts/\(pathSegment(postId))/ratings/\(pathSegment(topicId))")
    }

    static func setPostImages(postId: String, images: [CreatePostImageInput]) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/posts/\(pathSegment(postId))/images", body: SetPostImagesBody(images: images))
    }
}
