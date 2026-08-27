import VouchaAPI
import VouchaModels

extension NativeCommentThreadViewModel {
    func vote(postId: String, choice: ElectionVoteChoice?) async {
        guard let client, inFlightVotePostIds.insert(postId).inserted else { return }
        defer { inFlightVotePostIds.remove(postId) }
        let previousChoice = voteChoicesByPostId[postId]
        let previousPost = post(with: postId)
        reconcileVote(postId: postId, previous: previousChoice, next: choice)
        if let choice {
            voteChoicesByPostId[postId] = choice
        } else {
            voteChoicesByPostId.removeValue(forKey: postId)
        }
        await mutate(rollback: {
            self.restoreVote(postId: postId, previousPost: previousPost)
            if let previousChoice {
                self.voteChoicesByPostId[postId] = previousChoice
            } else {
                self.voteChoicesByPostId.removeValue(forKey: postId)
            }
        }, operation: {
            if let choice {
                let _: EmptyResponse = try await client.send(.votePost(postId: postId, choice: choice))
            } else {
                let _: EmptyResponse = try await client.send(.clearPostVote(postId: postId))
            }
        })
    }

    private func reconcileVote(postId: String, previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        guard let post = post(with: postId), let election = post.election else { return }
        let counts = ElectionVoteCountReconciler.reconcile(
            previous: previous ?? election.myVote,
            next: next,
            positive: election.votesCountUp,
            negative: election.votesCountDown
        )
        replace(post: post.hydrated(
            renderedHtml: nil,
            metrics: nil,
            election: .init(
                votesScoreNet: election.votesScoreNet,
                votesCountUp: counts.positive,
                votesCountDown: counts.negative,
                myVote: next
            ),
            voteChoice: next
        ))
    }

    private func restoreVote(postId _: String, previousPost: Post?) {
        guard let previousPost else { return }
        replace(post: previousPost)
    }

    private func post(with postId: String) -> Post? {
        if rootPost?.id == postId {
            return rootPost
        }
        return descendantPosts.first(where: { $0.id == postId })
            ?? ancestorPosts.first(where: { $0.id == postId })
    }

    private func replace(post: Post) {
        if rootPost?.id == post.id {
            rootPost = post
        }
        descendantPosts = descendantPosts.map { $0.id == post.id ? post : $0 }
        ancestorPosts = ancestorPosts.map { $0.id == post.id ? post : $0 }
        if let election = post.election {
            postElectionsById[post.id] = election
        }
        descendantPagination.replaceItems(descendantPosts)
        rebuildCommentTree()
    }
}
