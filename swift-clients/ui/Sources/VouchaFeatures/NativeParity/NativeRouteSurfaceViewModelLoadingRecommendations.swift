import Foundation
import VouchaAPI
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadTopicRecommendationRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if routeMatch?.path == "/topic-recommendations/create" {
            return [
                row(
                    "square.and.pencil",
                    appText(.nativeSwiftRouteSurfaceCreateRecommendation),
                    appText(.nativeSwiftRouteSurfaceCreateRecommendationDetail)
                ),
                row(
                    "text.cursor",
                    appText(.nativeSwiftRouteSurfaceTopicDetails),
                    appText(.nativeSwiftRouteSurfaceTopicDetailsEntry)
                )
            ]
        }
        if let recommendationId = routeMatch?.param("id") {
            let response: NativeTopicRecommendationDetailResponse = try await client.send(
                .topicRecommendation(id: recommendationId)
            )
            let post = response.post
            let title = recommendationTitle(post)
            return [
                row(
                    "lightbulb",
                    title.text,
                    rawText(post.markdown ?? post.id),
                    declaredLanguage: title.declaredLanguage,
                    detectedLanguage: title.detectedLanguage
                ),
                row(
                    "square.and.pencil",
                    appText(.nativeSwiftRouteSurfaceEditRecommendation),
                    appText(.nativeSwiftRouteSurfaceEditRecommendationDetail)
                )
            ]
        }
        let response: NativeTopicRecommendationsFeedResponse = try await client.send(
            .topicRecommendations(limit: 25)
        )
        return response.results.compactMap {
            guard let post = response.posts[$0.entityId ?? $0.id] else { return nil }
            let title = recommendationTitle(post)
            return row(
                "lightbulb",
                title.text,
                rawText(post.markdown ?? post.id),
                declaredLanguage: title.declaredLanguage,
                detectedLanguage: title.detectedLanguage
            )
        }
    }
}

private struct RecommendationTitle {
    let text: UiVerbatimText
    let declaredLanguage: String?
    let detectedLanguage: String?

    init(post: NativePostSummary) {
        if let title = post.title?.trimmingCharacters(in: .whitespacesAndNewlines), !title.isEmpty {
            text = .userContent(title)
            declaredLanguage = post.declaredLanguage
            detectedLanguage = post.linguaRsDetectedLanguage
            return
        }
        text = .userContent(post.slug ?? post.id)
        declaredLanguage = nil
        detectedLanguage = nil
    }
}

private extension NativeRouteSurfaceViewModel {
    func recommendationTitle(_ post: NativePostSummary) -> RecommendationTitle {
        RecommendationTitle(post: post)
    }
}
