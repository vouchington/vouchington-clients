import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeCommentThreadViewModel {
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
        postElectionsById = [rootPostId: detailResponse.postElection].compactMapValues { $0 }
        electionVotesById = [rootPostId: detailResponse.electionVote].compactMapValues { $0 }
        voteChoicesByPostId = [rootPostId: detailResponse.electionVote?.choice].compactMapValues { $0 }
        bookmarksByPostId = detailResponse.bookmarks ?? [:]
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

    func apply(ancestorsResponse: PostThreadEnvelope) {
        ancestorPosts = ancestorsResponse.results.compactMap {
            ancestorsResponse.posts[$0.entityId]?.withRenderedHtml(ancestorsResponse.markdownToHtml?[$0.entityId])
        }
        mergeThreadMetadata(from: ancestorsResponse)
    }

    private func mergeThreadMetadata(from response: PostThreadEnvelope) {
        postElectionsById.merge(response.postElections ?? [:]) { _, new in new }
        electionVotesById.merge(response.electionVotes ?? [:]) { _, new in new }
        voteChoicesByPostId.merge((response.electionVotes ?? [:]).mapValues(\.choice)) { _, new in new }
        bookmarksByPostId.merge(response.bookmarks ?? [:]) { _, new in new }
    }
}
