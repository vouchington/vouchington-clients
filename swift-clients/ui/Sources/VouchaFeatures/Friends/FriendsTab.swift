/// The two list modes for the Friends tab.
public enum FriendsTab: String, CaseIterable, Identifiable, Sendable {
    case following
    case followers

    public var id: String {
        rawValue
    }

    public var titleKey: UiMessageKey {
        switch self {
        case .following: .nativeSwiftNavigationTitlesFollowing
        case .followers: .nativeSwiftNavigationTitlesFollowers
        }
    }
}

import VouchaLocalization
