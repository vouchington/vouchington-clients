import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativePostComposeViewModel {
    var hasDraftContent: Bool {
        hasCoreText ||
            hasImages ||
            !linkURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ||
            reviewTopicRatings.contains(where: \.hasContent) ||
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
                images: imageInputs.isEmpty ? nil : imageInputs,
                turnstileToken: turnstileToken
            )
        case .dataPoint:
            Endpoint.createCommunityPost(
                communityIdOrSlug: communityIdOrSlug,
                postType: postType,
                title: title,
                markdown: bodyText,
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

    var draftRow: NativeRouteDestinationRow {
        switch postType {
        case .link:
            .init(
                icon: "link",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledLink)
                    : .verbatim(title),
                detail: linkURL.trimmedOrNil.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftPostComposeSavedLinkDraft)
            )
        case .review:
            .init(
                icon: "star",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledReview)
                    : .verbatim(title),
                detail: .count(reviewTopicRatings.count, item: "rating")
            )
        case .dataPoint:
            .init(
                icon: "chart.bar",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledDataPoint)
                    : .verbatim(title),
                detail: dataPointVertical.map { .message($0.titleKey) }
                    ?? .message(.nativeSwiftPostComposeSavedDataPointDraft)
            )
        default:
            .init(
                icon: "tray.and.arrow.down",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledDraft)
                    : .verbatim(title),
                detail: bodyText.trimmedOrNil.map(UiVerbatimText.verbatim)
                    ?? imageSummary
                    ?? .message(.nativeSwiftPostComposeSavedDraft)
            )
        }
    }

    private func makeReviewEndpoint(turnstileToken: String?) -> Endpoint {
        Endpoint.createPost(
            postType: postType,
            title: title,
            markdown: bodyText,
            reviewTopicRatings: reviewTopicRatings.filter(\.isValid).map {
                .init(topicId: $0.topicId.trimmingCharacters(in: .whitespacesAndNewlines), rating: $0.rating)
            },
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
        dataPointVertical = nil
        dataPointStructuredDataJSON = ""
        resetImageFields()
    }
}
