import VouchaModels

extension IntegrityPenaltyRow {
    var userDisplayName: String? {
        switch self {
        case let .report(penalty): penalty.userDisplayName
        case let .vote(penalty): penalty.userDisplayName
        }
    }

    var userUsername: String? {
        switch self {
        case let .report(penalty): penalty.userUsername
        case let .vote(penalty): penalty.userUsername
        }
    }

    var creatorDisplayName: String? {
        switch self {
        case let .report(penalty): penalty.creatorDisplayName
        case let .vote(penalty): penalty.creatorDisplayName
        }
    }

    var creatorUsername: String? {
        switch self {
        case let .report(penalty): penalty.creatorUsername
        case let .vote(penalty): penalty.creatorUsername
        }
    }

    var revokerDisplayName: String? {
        switch self {
        case let .report(penalty): penalty.revokerDisplayName
        case let .vote(penalty): penalty.revokerDisplayName
        }
    }

    var revokerUsername: String? {
        switch self {
        case let .report(penalty): penalty.revokerUsername
        case let .vote(penalty): penalty.revokerUsername
        }
    }

    var sourceFlagType: String? {
        switch self {
        case let .report(penalty): penalty.sourceFlagType
        case let .vote(penalty): penalty.sourceFlagType
        }
    }
}
