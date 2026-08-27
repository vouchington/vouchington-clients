import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    var canLoadMoreModmail: Bool {
        guard selectedTab == .modmail else { return false }
        if modmailThreadId == nil {
            return modmailThreadPagination.hasLoadedPage && modmailThreadPagination.hasMore
                && !modmailThreadPagination.isLoading
        }
        return modmailEndCursor != nil && !isLoadingMoreModmail
    }

    func loadMoreModmail() async {
        if modmailThreadId == nil {
            await loadMoreModmailThreads()
            return
        }
        guard client != nil, let after = modmailEndCursor, !isLoadingMoreModmail else { return }
        let revision = modmailLoadRevision
        modmailPageRequestRevision += 1
        let pageRevision = modmailPageRequestRevision
        let tab = selectedTab
        let threadId = modmailThreadId
        beginModmailPagination()
        defer {
            if modmailPageRequestRevision == pageRevision {
                finishModmailPagination()
            }
        }
        do {
            guard let modmailThreadId else { return }
            try await loadOlderModmailMessages(
                threadId: modmailThreadId,
                after: after,
                revision: revision,
                pageRevision: pageRevision,
                tab: tab
            )
        } catch {
            guard isCurrentModmailPage(
                revision: revision,
                pageRevision: pageRevision,
                tab: tab,
                threadId: threadId
            ) else { return }
            failModmailPagination()
        }
    }

    private func loadOlderModmailMessages(
        threadId: String,
        after: String,
        revision: Int,
        pageRevision: Int,
        tab: CommunitySurfaceTab
    ) async throws {
        guard let client else { return }
        let response: CommunityModmailMessagesResponse = try await client.send(
            .communityModmailMessages(idOrSlug: slug, conversationId: threadId, after: after, limit: 25)
        )
        guard isCurrentModmailPage(
            revision: revision,
            pageRevision: pageRevision,
            tab: tab,
            threadId: threadId
        ) else { return }
        let olderRows = response.results.compactMap { message -> NativeRouteDestinationRow? in
            guard modmailRowIds.insert(message.id).inserted else { return nil }
            return .init(
                icon: "bubble.left.and.text.bubble.right",
                title: message.senderUsername.map { .userContent("@\($0)") }
                    ?? .message(.nativeSwiftRebasedRouteSurfacesMessage),
                detail: .userContent(message.bodyText)
            )
        }
        summary.rows = olderRows + summary.rows
        modmailEndCursor = response.pageInfo.endCursor
    }

    private func loadMoreModmailThreads() async {
        guard let client, let request = modmailThreadPagination.beginNextPage() else { return }
        do {
            let response: CommunityModmailThreadsResponse = try await client.send(
                .communityModmail(idOrSlug: slug, after: request.cursor, limit: 25)
            )
            guard selectedTab == .modmail, modmailThreadId == nil,
                  modmailThreadPagination.isCurrent(request)
            else { return }
            modmailThreadPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            for result in response.results where modmailRowIds.insert(result.id).inserted {
                summary.rows.append(.init(
                    icon: "bubble.left.and.bubble.right",
                    title: .message(
                        .nativeSwiftRebasedRouteSurfacesThread,
                        textParameters: ["id": .verbatim(result.id)]
                    ),
                    detail: .message(.nativeSwiftRebasedRouteSurfacesOpen)
                ))
            }
            succeedModmailPagination()
        } catch {
            guard modmailThreadPagination.isCurrent(request) else { return }
            modmailThreadPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            failModmailPagination()
        }
    }

    private func isCurrentModmailPage(
        revision: Int,
        pageRevision: Int,
        tab: CommunitySurfaceTab,
        threadId: String?
    ) -> Bool {
        selectedTab == tab &&
            modmailThreadId == threadId &&
            modmailLoadRevision == revision &&
            modmailPageRequestRevision == pageRevision
    }
}
