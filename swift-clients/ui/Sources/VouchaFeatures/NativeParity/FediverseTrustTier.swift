import VouchaLocalization
import VouchaModels

enum FediverseTrustTier: Equatable {
    case trusted
    case neutral
    case distrusted
    case unrated

    init(election: HostnameElection?) {
        guard let election, election.votesCountUp + election.votesCountDown > 0 else {
            self = .unrated
            return
        }
        if election.votesScoreNet >= 3, election.votesCountUp >= 5 {
            self = .trusted
        } else if election.votesScoreNet <= -3 {
            self = .distrusted
        } else {
            self = .neutral
        }
    }

    var label: UiVerbatimText {
        switch self {
        case .trusted: .message(.nativeSwiftRouteSurfaceTrusted)
        case .neutral: .message(.nativeSwiftRouteSurfaceNeutral)
        case .distrusted: .message(.nativeSwiftRouteSurfaceDistrusted)
        case .unrated: .message(.nativeSwiftRouteSurfaceUnrated)
        }
    }
}
