public enum CommunityLimit: Equatable, Sendable {
    case membership
    case moderator
}

public extension VouchaError {
    var communityLimit: CommunityLimit? {
        let code: String?
        switch self {
        case let .api(409, value), let .apiMessage(409, value, _): code = value
        default: return nil
        }
        switch code {
        case "COMMUNITY_MEMBERSHIP_LIMIT_REACHED": return .membership
        case "COMMUNITY_MODERATOR_LIMIT_REACHED": return .moderator
        default: return nil
        }
    }
}
