import VouchaCore
import VouchaLocalization

func communityLimitMessage(_ error: Error, expected: CommunityLimit?, fallback: UiMessageKey) -> UiMessage {
    guard let expected, let limit = (error as? VouchaError)?.communityLimit, limit == expected else {
        return UiMessage(fallback)
    }
    switch limit {
    case .membership: return UiMessage(.nativeCommunitiesMembershipLimitReached)
    case .moderator: return UiMessage(.nativeCommunitiesModeratorLimitReached)
    }
}
