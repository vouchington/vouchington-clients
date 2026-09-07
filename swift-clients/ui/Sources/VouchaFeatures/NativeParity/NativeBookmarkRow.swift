import Foundation
import VouchaLocalization
import VouchaModels

struct NativeBookmarkRow: Identifiable, Equatable {
    enum Destination: Equatable {
        case path(String)
        case comment(id: String, rootId: String?)
    }

    struct InverseAction: Equatable {
        let entityType: String
        let predicate: String
        let label: UiMessageKey
    }

    let entityType: String
    let entityId: String
    let icon: String
    let title: UiVerbatimText
    let detail: UiVerbatimText
    let destination: Destination
    let inverseAction: InverseAction?
    let rank: Int
    let declaredLanguage: String?
    let detectedLanguage: String?

    init(
        entityType: String,
        entityId: String,
        icon: String,
        title: UiVerbatimText,
        detail: UiVerbatimText,
        destination: Destination,
        inverseAction: InverseAction?,
        rank: Int,
        declaredLanguage: String? = nil,
        detectedLanguage: String? = nil
    ) {
        self.entityType = entityType
        self.entityId = entityId
        self.icon = icon
        self.title = title
        self.detail = detail
        self.destination = destination
        self.inverseAction = inverseAction
        self.rank = rank
        self.declaredLanguage = declaredLanguage
        self.detectedLanguage = detectedLanguage
    }

    var id: String {
        "\(entityType):\(entityId)"
    }
}

extension NativeBookmarkRow {
    static func postPath(type: PostType, id: String, slug: String?) -> String? {
        let segment = type == .topicRecommendation ? id : (slug.flatMap { $0.isEmpty ? nil : $0 } ?? id)
        let kind: String
        switch type {
        case .discussion: kind = "discussion"
        case .review: kind = "review"
        case .dataPoint: kind = "data-point"
        case .article: kind = "article"
        case .blogPost: kind = "blog-post"
        case .story: kind = "story"
        case .link: kind = "link"
        case .topicRecommendation: kind = "topic-recommendations"
        case .comment: return nil
        }
        return "/\(kind)/\(segment)"
    }

    static func postTitle(type: PostType, title: String?) -> UiVerbatimText {
        if let title = title?.trimmingCharacters(in: .whitespacesAndNewlines), !title.isEmpty {
            return .userContent(title)
        }
        return switch type {
        case .review: .message(.nativeSwiftHouseholdsBookmarksUntitledReview)
        case .dataPoint: .message(.nativeSwiftHouseholdsBookmarksUntitledDataPoint)
        case .comment: .message(.nativeSwiftHouseholdsBookmarksUntitledComment)
        case .link: .message(.nativeSwiftHouseholdsBookmarksUntitledLink)
        default: .message(.nativeSwiftHouseholdsBookmarksUntitledDiscussion)
        }
    }

    static func rssItemPath(id: String, mediaType: String?) -> String {
        let path = switch mediaType {
        case "audio": "/podcast-episodes"
        case "video": "/videos"
        default: "/news"
        }
        return "\(path)?rss_item=\(urlQueryValue(id))"
    }

    private static func urlQueryValue(_ value: String) -> String {
        var components = URLComponents()
        components.queryItems = [.init(name: "rss_item", value: value)]
        return components.percentEncodedQuery?
            .split(separator: "=", maxSplits: 1)
            .last
            .map(String.init)?
            .replacingOccurrences(of: "+", with: "%2B") ?? value
    }
}
