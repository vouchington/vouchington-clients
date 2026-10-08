import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CommunityAutomodWorkspaceViewModel {
    func saveAction() async {
        guard canModerate, !isSavingAction, let client, let selectedAction else { return }
        isSavingAction = true
        notice = nil
        mutationError = nil
        defer { isSavingAction = false }
        do {
            let response: CommunityResponse = try await client.send(
                .updateCommunityAutomodSettings(idOrSlug: slug, automodAction: selectedAction)
            )
            guard canModerate, !Task.isCancelled else { return }
            committedAction = response.community.automodAction
            self.selectedAction = committedAction
            notice = UiMessage(.extractedCommunitiesCommunityAutomodActionFormAutomodActionSaved5a7127b1)
        } catch {
            guard canModerate, !Task.isCancelled else { return }
            if isAuthorizationFailure(error) {
                denyAccess()
            } else {
                mutationError =
                    UiMessage(.extractedCommunitiesCommunityAutomodActionFormCouldNotSaveTheAutomodActionD095dea5)
            }
        }
    }

    func dismiss(_ entry: CommunityModerationQueueEntry) async {
        guard canModerate, let client, entry.queueSource == CommunityModerationQueueSource.automodFlag.rawValue,
              let postId = entry.automodFlagPostId,
              pagination.items.contains(where: { $0.id == entry.id && $0.automodFlagPostId == postId }),
              dismissingPostIds.insert(postId).inserted else { return }
        notice = nil
        mutationError = nil
        defer { dismissingPostIds.remove(postId) }
        do {
            let _: EmptyResponse = try await client.send(.dismissCommunityAutomodFlag(idOrSlug: slug, postId: postId))
            guard canModerate, !Task.isCancelled else { return }
            pagination.remove { $0.id == entry.id }
            notice = UiMessage(.extractedCommunitiesCommunityAutomodFlagsPanelAutomodFlagDismissed66ee7e46)
            await refresh()
        } catch {
            guard canModerate, !Task.isCancelled else { return }
            if isAuthorizationFailure(error) {
                denyAccess()
            } else if isMissingFlag(error) {
                await refresh()
            } else {
                mutationError =
                    UiMessage(.extractedCommunitiesCommunityAutomodFlagsPanelFailedToDismissTheAutomodFlag0e50ec68)
            }
        }
    }

    private func isMissingFlag(_ error: Error) -> Bool {
        guard let error = error as? VouchaError else { return false }
        switch error {
        case .notFound, .api(statusCode: 404, _), .apiMessage(statusCode: 404, _, _): return true
        default: return false
        }
    }
}
