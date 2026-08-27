import VouchaModels

enum ElectionVoteCountReconciler {
    struct Counts: Equatable {
        let positive: Int
        let negative: Int
    }

    static func reconcile(
        previous: ElectionVoteChoice?,
        next: ElectionVoteChoice?,
        positive: Int,
        negative: Int
    ) -> Counts {
        .init(
            positive: max(0, positive - (previous.sign > 0 ? 1 : 0) + (next.sign > 0 ? 1 : 0)),
            negative: max(0, negative - (previous.sign < 0 ? 1 : 0) + (next.sign < 0 ? 1 : 0))
        )
    }
}

private extension ElectionVoteChoice? {
    var sign: Int {
        guard let choice = self else { return 0 }
        return switch choice {
        case .vouch, .like, .support, .confirm, .accurate: 1
        case .dislike, .disavow, .oppose, .dispute, .inaccurate: -1
        case .neutral: 0
        }
    }
}
