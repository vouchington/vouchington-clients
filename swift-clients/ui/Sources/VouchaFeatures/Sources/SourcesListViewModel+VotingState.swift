import VouchaModels

public extension SourcesListViewModel {
    func topicElection(for topicId: String?) -> TopicElection? {
        guard let topicId, let election = topicElectionsById[topicId] else { return nil }
        return TopicElection(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: election.votesCountUp,
            votesCountDown: election.votesCountDown,
            myVote: serverVotesByTopicId[topicId] ?? election.myVote
        )
    }
}
