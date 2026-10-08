import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class CommunityBrowseViewModel {
    var query = ""
    private(set) var state: CommunitySurfaceState = .idle
    private(set) var pagination = CursorPaginationState<CommunityBrowseItem>()

    var rows: [CommunityBrowseItem] {
        pagination.items
    }

    private let client: APIClient?

    init(client: APIClient?) {
        self.client = client
    }

    func load() async {
        await search(query: query)
    }

    func search(query: String) async {
        self.query = query
        pagination.reset()
        await loadNextPage()
    }

    func loadNextPage() async {
        guard let request = pagination.beginNextPage() else { return }
        state = .loading
        guard let client else {
            pagination.complete(request, items: [], endCursor: nil, hasNextPage: false)
            state = .loaded
            return
        }
        do {
            let response: CommunitiesSearchResponse = try await client.send(
                .communities(query: query.trimmedOrNil, after: request.cursor, limit: 25)
            )
            let newRows: [CommunityBrowseItem] = response.results.compactMap { result in
                guard let community = response.communities[result.id] else { return nil }
                let metrics = response.communityMetrics?[result.id]
                return CommunityBrowseItem(
                    id: community.id,
                    title: community.name,
                    detail: community.markdown ?? community.slug,
                    metrics: .count(metrics?.memberCount ?? 0, item: "member"),
                    provenance: community.provenance
                )
            }
            guard pagination.complete(
                request,
                items: newRows,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            state = .loaded
        } catch let error as VouchaError {
            guard pagination.fail(request, error: error) else { return }
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunities))
        } catch {
            guard pagination.fail(request, error: .unexpected(error.localizedDescription)) else { return }
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunities))
        }
    }
}
