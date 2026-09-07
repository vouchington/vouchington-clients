import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadPostRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        precondition(destination == .postDetail)
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
