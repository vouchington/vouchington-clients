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

    var contributionCanonicalIntent: String {
        let endpoint = makeCreateEndpoint(
            turnstileToken: nil,
            idempotencyKey: "00000000-0000-4000-8000-000000000000"
        )
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.outputFormatting = [.sortedKeys]
        let body = endpoint.body.flatMap { try? encoder.encode($0) }?.base64EncodedString() ?? ""
        return "\(endpoint.method.rawValue)\u{001F}\(endpoint.path)\u{001F}\(body)"
    }

    func makeCreateEndpoint(turnstileToken: String?, idempotencyKey: String) -> Endpoint {
        if let communityIdOrSlug {
            return makeCommunityCreateEndpoint(
                communityIdOrSlug: communityIdOrSlug,
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
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
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            )
        case .review:
            return makeReviewEndpoint(turnstileToken: turnstileToken, idempotencyKey: idempotencyKey)
        case .dataPoint:
            return Endpoint.createPost(
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
            return Endpoint.createPost(
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

    private func makeReviewEndpoint(turnstileToken: String?, idempotencyKey: String) -> Endpoint {
        Endpoint.createPost(
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
