import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeCommentThreadViewModel {
    public func loadMoreAncestors() async {
        guard let client, let target = focusedCommentId,
              let request = ancestorPagination.beginNextPage() else { return }
        do {
            let response: PostThreadEnvelope = try await client.send(
                .postAncestors(postId: target, after: request.cursor, limit: 5)
            )
            apply(ancestorsResponse: response, request: request)
        } catch is CancellationError {
            ancestorPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                ancestorPagination.cancel(request)
                return
            }
            ancestorPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                ancestorPagination.cancel(request)
                return
            }
            ancestorPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    public func loadMoreDescendants() async {
        guard let client, let request = descendantPagination.beginNextPage() else { return }
        do {
            let response: PostThreadEnvelope = try await client.send(
                .postDescendants(postId: rootPostId, after: request.cursor)
            )
            apply(descendantsResponse: response, request: request)
            rebuildCommentTree()
        } catch is CancellationError {
            descendantPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                descendantPagination.cancel(request)
                return
            }
            descendantPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                descendantPagination.cancel(request)
                return
            }
            descendantPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func apply(detailResponse: PostEnvelope) {
        rootPost = detailResponse.post.withRenderedHtml(detailResponse.html)
        postMetricsById = [rootPostId: detailResponse.postMetrics].compactMapValues { $0 }
        postElectionsById = [rootPostId: detailResponse.postElection].compactMapValues { $0 }
        electionVotesById = [rootPostId: detailResponse.electionVote].compactMapValues { $0 }
        voteChoicesByPostId = [rootPostId: detailResponse.electionVote?.choice].compactMapValues { $0 }
        bookmarksByPostId = detailResponse.bookmarks ?? [:]
        postEmbedsByPostId = detailResponse.linkEmbed.map { [rootPostId: $0] } ?? [:]
    }

    func resetThreadMetadata() {
        usersById = [:]
        postMetricsById = [:]
        postElectionsById = [:]
        electionVotesById = [:]
        voteChoicesByPostId = [:]
        bookmarksByPostId = [:]
        communitiesById = [:]
        storiesById = [:]
        rssFeedItemsById = [:]
        rssFeedItemElectionsById = [:]
        rssFeedItemThumbnailUrlsById = [:]
        relatedPostIdsByUrlId = [:]
        storyMemberIdsById = [:]
        storyPostIdsById = [:]
        pinnedPostIds = []
        postEmbedsByPostId = [:]
        rssFeedItemEmbedsById = [:]
    }

    func apply(descendantsResponse: PostThreadEnvelope, request: CursorPageRequest) {
        guard descendantPagination.isCurrent(request) else { return }
        let page = descendantsResponse.results.compactMap {
            descendantsResponse.posts[$0.entityId]?.withRenderedHtml(descendantsResponse.markdownToHtml?[$0.entityId])
        }
        mergeThreadMetadata(from: descendantsResponse)
        descendantPagination.complete(
            request,
            items: page,
            endCursor: descendantsResponse.pageInfo.endCursor,
            hasNextPage: descendantsResponse.pageInfo.hasNextPage
        )
        descendantPosts = descendantPagination.items
    }

    func apply(ancestorsResponse: PostThreadEnvelope, request: CursorPageRequest) {
        guard ancestorPagination.isCurrent(request) else { return }
        let page = ancestorsResponse.results.compactMap {
            ancestorsResponse.posts[$0.entityId]?.withRenderedHtml(ancestorsResponse.markdownToHtml?[$0.entityId])
        }
        mergeThreadMetadata(from: ancestorsResponse)
        ancestorPagination.complete(
            request,
            items: page,
            endCursor: ancestorsResponse.pageInfo.endCursor,
            hasNextPage: ancestorsResponse.pageInfo.hasNextPage,
            position: request.cursor == nil ? .append : .prepend
        )
        ancestorPosts = ancestorPagination.items.filter {
            $0.id != rootPostId && $0.id != focusedCommentId
        }
    }

    private func mergeThreadMetadata(from response: PostThreadEnvelope) {
        usersById.merge(response.users ?? [:]) { _, new in new }
        postMetricsById.merge(response.postsMetrics ?? [:]) { _, new in new }
        postElectionsById.merge(response.postElections ?? [:]) { _, new in new }
        electionVotesById.merge(response.electionVotes ?? [:]) { _, new in new }
        voteChoicesByPostId.merge((response.electionVotes ?? [:]).mapValues(\.choice)) { _, new in new }
        bookmarksByPostId.merge(response.bookmarks ?? [:]) { _, new in new }
        communitiesById.merge(response.communities ?? [:]) { _, new in new }
        storiesById.merge(response.stories ?? [:]) { _, new in new }
        rssFeedItemsById.merge(response.rssFeedItems ?? [:]) { _, new in new }
        rssFeedItemElectionsById.merge(response.rssFeedItemElections ?? [:]) { _, new in new }
        rssFeedItemThumbnailUrlsById.merge(response.rssFeedItemThumbnailUrl ?? [:]) { _, new in new }
        relatedPostIdsByUrlId.merge(response.relatedPostsByUrlId ?? [:]) { _, new in new }
        storyMemberIdsById.merge(response.storyMemberIds ?? [:]) { _, new in new }
        storyPostIdsById.merge(response.storyPostIds ?? [:]) { _, new in new }
        var knownPinnedPostIds = Set(pinnedPostIds)
        pinnedPostIds.append(contentsOf: (response.pinnedPostIds ?? []).filter {
            knownPinnedPostIds.insert($0).inserted
        })
        postEmbedsByPostId.merge(response.postLinkEmbeds ?? [:]) { _, new in new }
        rssFeedItemEmbedsById.merge(response.rssFeedItemEmbeds ?? [:]) { _, new in new }
    }
}
