import VouchaModels

public extension Endpoint {
    static func createCommunityPost(
        communityIdOrSlug: String,
        postType: PostType,
        title: String,
        markdown: String,
        slug: String? = nil,
        broadcast: BroadcastScope = .everyone,
        privacy: PostPrivacy = .public,
        isAnonymous: Bool = false,
        url: String? = nil,
        urlId: String? = nil,
        rootId: String? = nil,
        parentId: String? = nil,
        reviewTopicRatings: [CreatePostReviewTopicRatingInput]? = nil,
        images: [CreatePostImageInput]? = nil,
        dataPointVertical: DataPointVertical? = nil,
        structuredData: CreatePostJSONValue? = nil,
        declaredLanguage: String? = nil,
        turnstileToken: String? = nil,
        recaptchaToken: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(Endpoint.pathSegment(communityIdOrSlug))/posts",
            body: CreatePostBody(
                postType: postType,
                title: title,
                markdown: markdown,
                slug: slug,
                broadcast: broadcast,
                privacy: privacy,
                isAnonymous: isAnonymous,
                url: url,
                urlId: urlId,
                rootId: rootId,
                parentId: parentId,
                reviewTopicRatings: reviewTopicRatings,
                images: images,
                dataPointVertical: dataPointVertical,
                structuredData: structuredData,
                declaredLanguage: declaredLanguage,
                cfTurnstileResponse: turnstileToken,
                recaptchaToken: recaptchaToken
            )
        )
    }
}
