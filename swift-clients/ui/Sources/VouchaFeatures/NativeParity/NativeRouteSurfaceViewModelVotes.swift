import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    public func voteHostname(choice: ElectionVoteChoice?) async {
        guard let client, let hostnameId = hostnameDetailId, !hostnameVoteInFlight else { return }
        let previousVote = hostnameDetailVote
        let previousElection = hostnameDetailElection
        hostnameVoteInFlight = true
        defer { hostnameVoteInFlight = false }
        reconcileHostnameVote(previous: previousVote, next: choice)
        hostnameDetailVote = choice
        refreshHostnameTrustRow()
        let _: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
            hostnameDetailVote = previousVote
            hostnameDetailElection = previousElection
            refreshHostnameTrustRow()
        }, {
            if let choice {
                try await client.send(.voteHostname(hostnameId: hostnameId, choice: choice))
            } else {
                try await client.send(.clearHostnameVote(hostnameId: hostnameId))
            }
        })
    }

    func hostnameVoteSummary() -> TopicElection? {
        hostnameDetailElection.map {
            TopicElection(
                votesScoreNet: $0.votesScoreNet,
                votesCountUp: $0.votesCountUp,
                votesCountDown: $0.votesCountDown,
                myVote: hostnameDetailVote ?? $0.myVote
            )
        }
    }

    private func reconcileHostnameVote(previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        guard let election = hostnameDetailElection else { return }
        let counts = ElectionVoteCountReconciler.reconcile(
            previous: previous ?? election.myVote,
            next: next,
            positive: election.votesCountUp,
            negative: election.votesCountDown
        )
        hostnameDetailElection = TopicElection(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: counts.positive,
            votesCountDown: counts.negative,
            myVote: next
        )
    }

    /// Cast, change, or retract a vote on a topic detail surface.
    public func vote(topicId: String, choice: ElectionVoteChoice?) async {
        guard let client else { return }
        guard !inFlightVoteTopicIds.contains(topicId) else { return }
        let previous = myVotesByTopicId[topicId]
        let previousElection = topicDetailElection
        inFlightVoteTopicIds.insert(topicId)
        defer { inFlightVoteTopicIds.remove(topicId) }
        reconcileVote(topicId: topicId, previous: previous, next: choice)
        if let choice {
            myVotesByTopicId[topicId] = choice
        } else {
            myVotesByTopicId.removeValue(forKey: topicId)
        }
        let _: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
            topicDetailElection = previousElection
            if let previous {
                myVotesByTopicId[topicId] = previous
            } else {
                myVotesByTopicId.removeValue(forKey: topicId)
            }
        }, {
            if let choice {
                try await client.send(.voteTopic(topicId: topicId, choice: choice))
            } else {
                try await client.send(.clearTopicVote(topicId: topicId))
            }
        })
    }

    private func reconcileVote(topicId: String, previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        guard topicDetailId == topicId, let election = topicDetailElection else { return }
        let counts = ElectionVoteCountReconciler.reconcile(
            previous: previous ?? election.myVote,
            next: next,
            positive: election.votesCountUp,
            negative: election.votesCountDown
        )
        topicDetailElection = .init(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: counts.positive,
            votesCountDown: counts.negative,
            myVote: next
        )
    }

    func topicVoteSummary(for topicId: String) -> TopicElection? {
        guard let election = topicDetailElection, topicDetailId == topicId else { return nil }
        let myVote = myVotesByTopicId[topicId] ?? election.myVote
        return TopicElection(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: election.votesCountUp,
            votesCountDown: election.votesCountDown,
            myVote: myVote
        )
    }

    func applyTopicDetailResponse(_ response: NativeTopicDetailResponse) {
        topicDetailId = response.topic.id
        topicDetailElection = response.topicElection.map { election in
            let myVote = response.electionVote?.choice ?? election.myVote
            if let myVote {
                myVotesByTopicId[response.topic.id] = myVote
            }
            return TopicElection(
                votesScoreNet: election.votesScoreNet,
                votesCountUp: election.votesCountUp,
                votesCountDown: election.votesCountDown,
                myVote: myVote
            )
        }
    }
}
