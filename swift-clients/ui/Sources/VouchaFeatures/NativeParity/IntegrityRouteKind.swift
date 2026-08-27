enum IntegrityRouteKind: String, CaseIterable {
    case reportFlags = "/report-integrity/flags"
    case reportPenalties = "/report-integrity/penalties"
    case voteFlags = "/vote-integrity/flags"
    case votePenalties = "/vote-integrity/penalties"

    init?(path: String?) {
        guard let path else { return nil }
        self.init(rawValue: path)
    }
}

enum IntegrityDomain {
    case report, vote

    var flagsPath: String {
        switch self {
        case .report: "/report-integrity/flags"
        case .vote: "/vote-integrity/flags"
        }
    }

    var penaltiesPath: String {
        switch self {
        case .report: "/report-integrity/penalties"
        case .vote: "/vote-integrity/penalties"
        }
    }
}
