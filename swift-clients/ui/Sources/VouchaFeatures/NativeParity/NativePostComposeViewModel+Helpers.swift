import Foundation
import VouchaAPI
import VouchaModels

extension NativePostComposeViewModel {
    var hasDraftContent: Bool {
        hasCoreText ||
            hasImages ||
            !linkURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ||
            reviewTopicRatings.contains(where: \.hasContent) ||
            categoryDrafts.contains(where: { !$0.value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) ||
            dataPointVertical != nil ||
            !dataPointStructuredDataJSON.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var hasCoreText: Bool {
        !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ||
            !bodyText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var hasValidReviewRatings: Bool {
        let filledRatings = reviewTopicRatings.filter(\.isValid)
        return !filledRatings.isEmpty && filledRatings.count == reviewTopicRatings.count
    }

    var hasValidDiscussionCategoryDrafts: Bool {
        guard postType == .discussion else { return true }
        return categoryDrafts.allSatisfy { draft in
            guard draft.type == .hashtag else { return true }
            let value = draft.value.trimmingCharacters(in: .whitespacesAndNewlines)
            return value.isEmpty || CanonicalHashtagSlug(rawValue: draft.value) != nil
        }
    }

    var postCategoryInputs: [PostCategoryInput]? {
        guard postType == .discussion else { return nil }
        let inputs = categoryDrafts.compactMap { draft -> PostCategoryInput? in
            switch draft.type {
            case .topic:
                guard let topicId = draft.value.trimmedOrNil else { return nil }
                return .topic(topicId: topicId)
            case .hashtag:
                return PostCategoryInput(hashtag: draft.value)
            }
        }
        return inputs.isEmpty ? nil : inputs
    }

    func makeCreateEndpoint(turnstileToken: String?) -> Endpoint {
        if let communityIdOrSlug {
            return makeCommunityCreateEndpoint(communityIdOrSlug: communityIdOrSlug, turnstileToken: turnstileToken)
        }

        switch postType {
        case .link:
            return Endpoint.createPost(
                postType: postType,
                title: title,
                markdown: bodyText,
                url: linkURL.trimmedOrNil,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken
            )
        case .review:
            return makeReviewEndpoint(turnstileToken: turnstileToken)
        case .dataPoint:
            return Endpoint.createPost(
                postType: postType,
                title: title,
                markdown: bodyText,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                dataPointVertical: dataPointVertical,
                structuredData: parseStructuredData(),
                turnstileToken: turnstileToken
            )
        default:
            return Endpoint.createPost(
                postType: postType,
                title: title,
                markdown: bodyText,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken
            )
        }
    }

    private func makeCommunityCreateEndpoint(communityIdOrSlug: String, turnstileToken: String?) -> Endpoint {
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
                turnstileToken: turnstileToken
            )
        case .review:
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
                turnstileToken: turnstileToken
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
                turnstileToken: turnstileToken
            )
        default:
            Endpoint.createCommunityPost(
                communityIdOrSlug: communityIdOrSlug,
                postType: postType,
                title: title,
                markdown: bodyText,
                categories: postCategoryInputs,
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken
            )
        }
    }

    func resetFormAfterPublish() {
        resetCommonFields()
        resetTypeSpecificFields()
    }

    func resetFormAfterSave() {
        resetCommonFields()
        resetTypeSpecificFields()
    }

    func parseStructuredData() -> CreatePostJSONValue? {
        let trimmed = dataPointStructuredDataJSON.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return try? CreatePostJSONValue.parse(jsonString: trimmed)
    }

    private func makeReviewEndpoint(turnstileToken: String?) -> Endpoint {
        Endpoint.createPost(
            postType: postType,
            title: title,
            markdown: bodyText,
            reviewTopicRatings: reviewTopicRatings.filter(\.isValid).map {
                .init(topicId: $0.topicId.trimmingCharacters(in: .whitespacesAndNewlines), rating: $0.rating)
            },
            categories: postCategoryInputs,
            images: imageInputs.isEmpty ? nil : imageInputs,
            turnstileToken: turnstileToken
        )
    }

    private func resetCommonFields() {
        title = ""
        bodyText = ""
        linkURL = ""
    }

    private func resetTypeSpecificFields() {
        turnstileToken = nil
        reviewTopicRatings = [.init()]
        categoryDrafts = []
        dataPointVertical = nil
        dataPointStructuredDataJSON = ""
        resetImageFields()
    }
}
