/// Filter options for the Posts feed.
public enum PostFilter: String, CaseIterable, Identifiable, Sendable {
    case all
    case discussions
    case reviews

    public var id: String {
        rawValue
    }

    public var titleKey: UiMessageKey {
        switch self {
        case .all: .nativeSwiftNavigationTitlesAll
        case .discussions: .nativeSwiftNavigationTitlesDiscussions
        case .reviews: .nativeSwiftNavigationTitlesReviews
        }
    }

    /// The `post_types` query parameter value, or nil for no filter.
    var postTypes: String? {
        switch self {
        case .all: nil
        case .discussions: "discussion"
        case .reviews: "review"
        }
    }
}

import VouchaLocalization
