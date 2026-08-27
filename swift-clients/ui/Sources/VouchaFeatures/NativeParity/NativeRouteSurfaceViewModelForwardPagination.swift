import VouchaAPI
import VouchaCore

extension NativeRouteSurfaceViewModel {
    func loadInitialForwardRowsIfSupported(client: APIClient) async throws -> Bool {
        guard supportsForwardPagination else { return false }
        forwardPagination.reset()
        guard let request = forwardPagination.beginInitialPageIfNeeded() else { return true }
        do {
            let page = try await loadForwardPage(client: client, after: request.cursor)
            guard forwardPagination.complete(
                request,
                items: page.rows,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return true }
            rows = forwardPagination.items.map(\.row)
        } catch let error as VouchaError {
            _ = forwardPagination.fail(request, error: error)
            throw error
        } catch {
            let error = VouchaError.api(statusCode: 0, preconditionCode: nil)
            _ = forwardPagination.fail(request, error: error)
            throw error
        }
        return true
    }

    public func loadMoreForwardRows() async {
        guard let client, supportsForwardPagination,
              let request = forwardPagination.beginNextPage()
        else { return }
        do {
            let page = try await loadForwardPage(client: client, after: request.cursor)
            guard forwardPagination.complete(
                request,
                items: page.rows,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return }
            rows = forwardPagination.items.map(\.row)
        } catch let error as VouchaError {
            _ = forwardPagination.fail(request, error: error)
        } catch {
            _ = forwardPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

    private var supportsForwardPagination: Bool {
        guard focusedRssFeedItemId == nil else { return false }
        return switch destination {
        case .feedPosts, .postsBrowse, .storiesBrowse,
             .feedNews, .feedPodcasts, .feedVideos,
             .notifications, .feedReferralLinks, .lists, .referrals,
             .usersBrowse:
            true
        case .moderationReports, .moderationAppeals, .moderationDisputes,
             .moderationAdmin, .moderationIntegrity, .moderationCases:
            moderationPathSupportsForwardPagination
        default:
            false
        }
    }

    private var moderationPathSupportsForwardPagination: Bool {
        guard let path = routeMatch?.path else { return true }
        return path == "/appeals"
            || path == "/admin/modlog"
            || path.contains("disputes")
            || path.contains("vote-integrity")
            || path.contains("report-integrity")
    }

    private func loadForwardPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        switch destination {
        case .feedPosts, .postsBrowse, .storiesBrowse:
            try await loadPostPage(client: client, after: after)
        case .feedNews, .feedPodcasts, .feedVideos:
            try await loadRssFeedItemPage(client: client, after: after)
        case .notifications:
            try await loadNotificationPage(client: client, after: after)
        case .feedReferralLinks:
            try await loadReferralFeedPage(client: client, after: after)
        case .lists:
            try await loadListsPreviewPage(client: client, after: after)
        case .referrals:
            try await loadReferralPage(client: client, after: after)
        case .usersBrowse:
            try await loadUsersBrowsePage(client: client, after: after)
        case .moderationReports, .moderationAppeals, .moderationDisputes,
             .moderationAdmin, .moderationIntegrity, .moderationCases:
            try await loadModerationForwardPage(client: client, after: after)
        default:
            preconditionFailure("Forward pagination requested for an unsupported destination")
        }
    }
}
