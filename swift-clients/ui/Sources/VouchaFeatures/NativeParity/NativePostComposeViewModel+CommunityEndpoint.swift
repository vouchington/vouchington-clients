import Foundation
import VouchaAPI

extension NativePostComposeViewModel {
    func makeCommunityCreateEndpoint(
        communityIdOrSlug: String,
        turnstileToken: String?,
        idempotencyKey: String
    ) -> Endpoint {
        switch postType {
        case .link:
            Endpoint.createCommunityPost(
                communityIdOrSlug: communityIdOrSlug,
                postType: postType,
                title: title,
                markdown: bodyText,
                url: linkURL.trimmedOrNil,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
        case .review:
            makeCommunityReviewEndpoint(
                communityIdOrSlug: communityIdOrSlug,
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
        case .dataPoint:
            Endpoint.createCommunityPost(
                communityIdOrSlug: communityIdOrSlug,
                postType: postType,
                title: title,
                markdown: bodyText,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                dataPointVertical: dataPointVertical,
                structuredData: parseStructuredData(),
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
        default:
            Endpoint.createCommunityPost(
                communityIdOrSlug: communityIdOrSlug,
                postType: postType,
                title: title,
                markdown: bodyText,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
        }
    }

    private func makeCommunityReviewEndpoint(
        communityIdOrSlug: String,
        turnstileToken: String?,
        idempotencyKey: String
    ) -> Endpoint {
        Endpoint.createCommunityPost(
            communityIdOrSlug: communityIdOrSlug,
            postType: postType,
            title: title,
            markdown: bodyText,
            reviewTopicRatings: reviewTopicRatings.filter(\.isValid).map {
                .init(topicId: $0.topicId.trimmingCharacters(in: .whitespacesAndNewlines), rating: $0.rating)
            },
            categories: postCategoryInputs,
            images: imageInputs.isEmpty ? nil : imageInputs,
            turnstileToken: turnstileToken,
            idempotencyKey: idempotencyKey
        )
    }
}
