import Foundation
import VouchaAPI
import VouchaCore

public extension NativeRouteSurfaceViewModel {
    func load() async {
        guard let client, let destination else { return }
        switch state {
        case .idle, .error:
            break
        case .loading, .loaded:
            return
        }
        guard destination.supportsRemoteNativeSurface else { return }

        prepareForLoad(preservingProfileHeader: destination == .userProfile && userProfile.header != nil)
        let moderationTransparencyLoadRevision: Int?
        if destination == .moderationTransparency {
            self.moderationTransparencyLoadRevision += 1
            moderationTransparencyPageRevision += 1
            moderationTransparencyContinuationToken += 1
            moderationTransparencyIsLoadingOlder = false
            moderationTransparencyLoadMoreError = nil
            moderationTransparencyLoadRevision = self.moderationTransparencyLoadRevision
        } else {
            moderationTransparencyLoadRevision = nil
        }
        await performLoad(
            destination: destination,
            client: client,
            moderationTransparencyLoadRevision: moderationTransparencyLoadRevision
        )
    }

    private func performLoad(
        destination: NativeRouteDestinationIdentifier,
        client: APIClient,
        moderationTransparencyLoadRevision: Int?
    ) async {
        do {
            let loadedForwardRows = try await loadInitialForwardRowsIfSupported(client: client)
            if !loadedForwardRows {
                let loadedRows = try await loadRows(for: destination, client: client)
                guard ownsInitialModerationTransparencyLoad(moderationTransparencyLoadRevision) else { return }
                rows = loadedRows
            }
            guard ownsInitialModerationTransparencyLoad(moderationTransparencyLoadRevision) else { return }
            state = .loaded
        } catch let error as VouchaError {
            guard ownsInitialModerationTransparencyLoad(moderationTransparencyLoadRevision) else { return }
            state = .error(error)
        } catch {
            guard ownsInitialModerationTransparencyLoad(moderationTransparencyLoadRevision) else { return }
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    private func ownsInitialModerationTransparencyLoad(_ revision: Int?) -> Bool {
        revision == nil || revision == moderationTransparencyLoadRevision
    }

    private func prepareForLoad(preservingProfileHeader: Bool) {
        state = .loading
        resetBookmarkDestinationResolution()
        bookmarkRows = []
        bookmarkMutationErrorMessage = nil
        currentBookmarkCollection = nil
        crawlHistoryPagination.reset()
        crawlHistoryPrefix = []
        crawlHistorySuffix = []
        crawlHistoryEndpoint = nil
        focusedRssFeedItem = nil
        focusedRssFeedItemContentHtml = nil
        focusedRssFeedItemElection = nil
        focusedRssFeedItemBookmarks = [:]
        focusedRssFeedItemVote = nil
        hostnameDetailId = nil
        hostnameDetailElection = nil
        hostnameDetailVote = nil
        hostnameVoteInFlight = false
        userProfile.pagination.reset()
        userProfile.collection = .none
        if !preservingProfileHeader {
            userProfile.header = nil
            userProfile.scope = nil
            detailRelationEntityType = nil
            detailRelationEntityId = nil
            detailRelationBookmarks = [:]
            detailRelationIsSelfProfile = false
            detailUserTags = []
            detailUserTagVotes = [:]
            detailReportTarget = nil
        }
        detailReportOperationGeneration += 1
        detailReportSubmissionState = .idle
        detailReportSuccessPresented = false
        detailReportErrorMessage = nil
        clearPendingReportDraft()
    }
}
