import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadPostRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        if destination == .postDetail {
            return try await loadPostDetailRows(client: client)
        }

        let postTypes = postTypesFilter(for: destination)
        var publicPostQuery = [
            URLQueryItem(name: "limit", value: "10"),
            URLQueryItem(name: "sort", value: "hot")
        ]
        if let postTypes {
            publicPostQuery.append(URLQueryItem(name: "post_types", value: postTypes))
        }
        let endpoint = destination == .postsBrowse || destination == .storiesBrowse
            ? Endpoint(.GET, path: "/api/v1/posts", queryItems: publicPostQuery)
            : Endpoint.posts(feedType: postFeedType, limit: 10, postTypes: postTypes)
        let response: NativePostFeedResponse = try await client.send(endpoint)
        return response.results.compactMap { result in
            guard let post = response.posts[result.entityId ?? result.id] else { return nil }
            let subtitle: UiVerbatimText = if let userId = result.sharedByUserId {
                appText(.nativeSwiftRouteSurfaceSharedByUser, parameters: ["user": userId])
            } else if let userId = post.createdById {
                appText(.nativeSwiftRouteSurfaceByUser, parameters: ["user": userId])
            } else if let deliveryType = result.deliveryType {
                postTypeText(deliveryType)
            } else {
                postTypeText(post.postType)
            }
            let title = NormalizedAuthoredText(
                text: post.title,
                declaredLanguage: post.declaredLanguage,
                detectedLanguage: post.linguaRsDetectedLanguage
            )
            return row(
                postTypeIcon(for: post.postType),
                title?.text ?? post.slug.map(rawText) ?? postTypeText(post.postType),
                subtitle,
                declaredLanguage: title?.declaredLanguage,
                detectedLanguage: title?.detectedLanguage
            )
        }
    }

    private func loadPostDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let idOrSlug = routeMatch?.param("id") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativePostDetailResponse = try await client.send(.post(idOrSlug: idOrSlug))
        let post = response.post
        let title = NormalizedAuthoredText(
            text: post.title,
            declaredLanguage: post.declaredLanguage,
            detectedLanguage: post.linguaRsDetectedLanguage
        )
        let detail = NormalizedAuthoredText(
            text: post.markdown,
            declaredLanguage: post.declaredLanguage,
            detectedLanguage: post.linguaRsDetectedLanguage
        )
        return [
            row(
                postTypeIcon(for: post.postType),
                title?.text ?? post.slug.map(rawText) ?? postTypeText(post.postType),
                rawText(post.markdown ?? post.createdById ?? post.id),
                declaredLanguage: title?.declaredLanguage,
                detectedLanguage: title?.detectedLanguage,
                detailDeclaredLanguage: detail?.declaredLanguage,
                detailDetectedLanguage: detail?.detectedLanguage
            )
        ]
    }
}
