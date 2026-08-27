enum IntegrityViewerTier: Equatable {
    case anonymous
    case member
    case siteModerator
    case customerSupport
    case administrator

    init(
        isSignedIn: Bool,
        isAdministrator: Bool,
        isSiteModerator: Bool,
        isCustomerSupport: Bool
    ) {
        if !isSignedIn {
            self = .anonymous
        } else if isAdministrator {
            self = .administrator
        } else if isSiteModerator {
            self = .siteModerator
        } else if isCustomerSupport {
            self = .customerSupport
        } else {
            self = .member
        }
    }

    var canReviewIntegrity: Bool {
        self == .administrator
    }

    var canReviewIntegrityPenalties: Bool {
        self == .administrator
    }

    var canResolveIntegrityFlag: Bool {
        self == .administrator
    }

    var canApplyIntegrityPenalty: Bool {
        self == .administrator
    }

    var canRevokeIntegrityPenalty: Bool {
        self == .administrator
    }
}
