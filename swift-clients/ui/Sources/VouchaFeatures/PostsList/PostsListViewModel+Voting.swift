import VouchaAPI
import VouchaModels

extension PostsListViewModel {
    /// Cast, change, or retract a vote on a post, applying the change optimistically.
    public func vote(postId: String, choice: ElectionVoteChoice?) async {
        guard !inFlightVotePostIds.contains(postId) else { return }
        let previous = myVotesByPostId[postId]
        let previousPost = pagination.items.first(where: { $0.id == postId })
        inFlightVotePostIds.insert(postId)
        defer { inFlightVotePostIds.remove(postId) }
        reconcileVote(postId: postId, previous: previous, next: choice)
        myVotesByPostId[postId] = choice
        let _: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
            restoreVote(postId: postId, previousPost: previousPost)
            self.myVotesByPostId[postId] = previous
        }, {
            if let choice {
                try await client.send(.votePost(postId: postId, choice: choice))
            } else {
                try await client.send(.clearPostVote(postId: postId))
            }
        })
    }

    private func reconcileVote(postId: String, previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        pagination.replaceItems(pagination.items.map { post in
            guard post.id == postId, let election = post.election else { return post }
            let counts = ElectionVoteCountReconciler.reconcile(
                previous: previous ?? election.myVote,
                next: next,
                positive: election.votesCountUp,
                negative: election.votesCountDown
            )
            return post.hydrated(
                renderedHtml: nil,
                metrics: nil,
                election: .init(
                    votesScoreNet: election.votesScoreNet,
                    votesCountUp: counts.positive,
                    votesCountDown: counts.negative,
                    myVote: next
                ),
                voteChoice: next
            )
        })
    }

    private func restoreVote(postId: String, previousPost: Post?) {
        guard let previousPost else { return }
        pagination.replaceItems(pagination.items.map { $0.id == postId ? previousPost : $0 })
    }
}
