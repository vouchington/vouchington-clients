import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class CommunityAutomodWorkspaceViewModel {
    var pagination = CursorPaginationState<CommunityModerationQueueEntry>()
    var canModerate: Bool
    var selectedAction: CommunityAutomodActionSetting?
    var committedAction: CommunityAutomodActionSetting?
    var isSavingAction = false
    var dismissingPostIds: Set<String> = []
    var notice: UiMessage?
    var mutationError: UiMessage?
    var replaceNextPage = true
    let client: APIClient?
    let slug: String

    init(client: APIClient?, slug: String, action: CommunityAutomodActionSetting?, canModerate: Bool) {
        self.client = client
        self.slug = slug
        self.canModerate = canModerate
        selectedAction = action
        committedAction = action
    }

    func loadNextPage() async {
        guard canModerate, let client, let request = pagination.beginNextPage() else { return }
        let replacing = replaceNextPage
        do {
            let response: CommunityModerationQueueResponse = try await client.send(
                .communityModerationQueue(idOrSlug: slug, after: request.cursor, limit: 20, source: .automodFlag)
            )
            guard pagination.isCurrent(request), !Task.isCancelled else {
                pagination.cancel(request)
                return
            }
            guard response.viewerTier == .moderator else {
                denyAccess()
                return
            }
            if replacing { pagination.replaceItems([]) }
            pagination.complete(
                request,
                items: response.entries
                    .filter { $0.queueSource == CommunityModerationQueueSource.automodFlag.rawValue },
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            replaceNextPage = false
        } catch {
            guard pagination.isCurrent(request) else { return }
            if Task.isCancelled {
                pagination.cancel(request)
            } else if isAuthorizationFailure(error) {
                denyAccess()
            } else {
                pagination.fail(request, error: (error as? VouchaError) ?? .unexpected(error.localizedDescription))
            }
        }
    }

    func refresh() async {
        let preserved = pagination.items
        pagination.reset()
        pagination.replaceItems(preserved)
        replaceNextPage = true
        await loadNextPage()
    }

    func denyAccess() {
        canModerate = false
        pagination.reset()
        dismissingPostIds = []
        notice = nil
        mutationError = nil
    }

    func isAuthorizationFailure(_ error: Error) -> Bool {
        guard let error = error as? VouchaError else { return false }
        switch error {
        case .unauthorized, .sessionExpired, .forbidden: return true
        case .api(statusCode: 401, _), .api(statusCode: 403, _),
             .apiMessage(statusCode: 401, _, _), .apiMessage(statusCode: 403, _, _): return true
        default: return false
        }
    }
}
