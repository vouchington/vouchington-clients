import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadModerationRows(
        client: APIClient,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws -> [NativeRouteDestinationRow] {
        async let pendingPosts: NativeCommunityPostsResponse = client.send(
            .communityPendingPosts(idOrSlug: slug, limit: 10)
        )
        async let pendingReports: CommunityPendingReportsResponse = client.send(
            .communityPendingReports(idOrSlug: slug)
        )
        let posts = try await pendingPosts
        let reports = try await pendingReports
        guard isCurrentCommunityLoad(revision, tab: tab) else { throw CancellationError() }
        pendingReportPagination.reset(items: reports.reports)
        pendingReportPagination.restoreContinuation(
            endCursor: reports.pageInfo.endCursor,
            hasMore: reports.pageInfo.hasNextPage
        )
        return [
            .init(
                icon: "checklist",
                title: UiMessage(.nativeSwiftCommunitiesPendingPosts),
                detail: UiMessage(
                    .nativeSwiftCommunitiesQueuedCount,
                    numberParameters: ["count": Double(posts.results.count)]
                )
            ),
            .init(
                icon: "exclamationmark.triangle",
                title: UiMessage(.nativeSwiftCommunitiesPendingReports),
                detail: UiMessage(
                    .nativeSwiftCommunitiesQueuedCount,
                    numberParameters: ["count": Double(reports.reports.count)]
                )
            )
        ]
    }

    func loadMorePendingReports() async {
        guard selectedTab == .moderation, let client,
              let request = pendingReportPagination.beginNextPage() else { return }
        do {
            let response: CommunityPendingReportsResponse = try await client.send(
                .communityPendingReports(idOrSlug: slug, after: request.cursor)
            )
            guard selectedTab == .moderation else {
                pendingReportPagination.cancel(request)
                return
            }
            guard pendingReportPagination.complete(
                request,
                items: response.reports,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            replacePendingReportCount()
        } catch is CancellationError {
            pendingReportPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                pendingReportPagination.cancel(request)
                return
            }
            pendingReportPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                pendingReportPagination.cancel(request)
                return
            }
            pendingReportPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    private func replacePendingReportCount() {
        guard let index = summary.rows.firstIndex(where: { $0.icon == "exclamationmark.triangle" }) else { return }
        summary.rows[index] = .init(
            icon: "exclamationmark.triangle",
            title: UiMessage(.nativeSwiftCommunitiesPendingReports),
            detail: UiMessage(
                .nativeSwiftCommunitiesQueuedCount,
                numberParameters: ["count": Double(pendingReportPagination.items.count)]
            )
        )
    }
}
