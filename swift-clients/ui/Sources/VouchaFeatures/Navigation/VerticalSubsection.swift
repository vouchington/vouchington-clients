/// The feed-scope sub-option within a vertical: personalized feed vs. global feed.
public enum FeedScope: String, CaseIterable, Identifiable, Hashable, Sendable {
    case your
    case all

    public var id: String {
        rawValue
    }

    public var systemImage: String {
        switch self {
        case .your: "person.crop.circle"
        case .all: "globe"
        }
    }
}

/// The sources-scope sub-option within a vertical: followed sources vs. all sources.
public enum SourceScope: String, CaseIterable, Identifiable, Hashable, Sendable {
    case your
    case all

    public var id: String {
        rawValue
    }

    public var systemImage: String {
        switch self {
        case .your: "star"
        case .all: "antenna.radiowaves.left.and.right"
        }
    }
}

/// A sub-option within a content vertical (News/Podcasts/Videos/Posts).
public enum VerticalSubsection: Hashable, Identifiable, Sendable {
    case feed(FeedScope)
    case sources(SourceScope)

    public var id: String {
        switch self {
        case let .feed(scope): "feed-\(scope.rawValue)"
        case let .sources(scope): "sources-\(scope.rawValue)"
        }
    }

    public var systemImage: String {
        switch self {
        case let .feed(scope): scope.systemImage
        case let .sources(scope): scope.systemImage
        }
    }
}
