import Foundation
import VouchaModels

public enum ImportExportRoute: Sendable, Equatable {
    case topics
    case sources(initialExportFilter: SourceFeedType?)

    public init?(path: String) {
        switch path {
        case "/my/topics/import-export": self = .topics
        case "/my/news-sources/import-export": self = .sources(initialExportFilter: .article)
        case "/my/podcasts/import-export": self = .sources(initialExportFilter: .podcast)
        case "/my/channels/import-export": self = .sources(initialExportFilter: .video)
        case "/my/sources/import-export": self = .sources(initialExportFilter: nil)
        default: return nil
        }
    }

    public var isTopics: Bool {
        if case .topics = self {
            return true
        }
        return false
    }

    public var initialExportFilter: SourceFeedType? {
        if case let .sources(filter) = self {
            return filter
        }
        return nil
    }
}
