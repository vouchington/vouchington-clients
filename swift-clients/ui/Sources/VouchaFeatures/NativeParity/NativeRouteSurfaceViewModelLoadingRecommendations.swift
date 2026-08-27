import VouchaAPI

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
            return [
                row(
                    "lightbulb",
                    rawText(post.title ?? post.slug ?? post.id),
                    rawText(post.markdown ?? post.id)
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
            return row(
                "lightbulb",
                rawText(post.title ?? post.slug ?? post.id),
                rawText(post.markdown ?? post.id)
            )
        }
    }
}
