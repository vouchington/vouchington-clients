/// Topic collection counts visible to the current viewer, with independent lower-bound flags.
public struct TopicViewerMetrics: Decodable, Sendable {
    public let count: [String: Int]?
    public let countCapped: [String: Bool]?
    public let viewerCount: [String: Int]?
    public let viewerCountCapped: [String: Bool]?

    public func visibleCount(_ key: String) -> Int {
        count?[key] ?? 0
    }

    public func isVisibleCountCapped(_ key: String) -> Bool {
        countCapped?[key] == true || viewerCountCapped?[key] == true
            || (viewerCount?[key] ?? 0) > visibleCount(key)
    }

    public var visiblePostsCount: Int {
        ["discussions", "reviews", "data-points"].reduce(0) { $0 + visibleCount($1) }
    }

    public var isVisiblePostsCountCapped: Bool {
        let categories = ["discussions", "reviews", "data-points"]
        return categories.contains { countCapped?[$0] == true || viewerCountCapped?[$0] == true }
            || categories.reduce(0) { $0 + (viewerCount?[$1] ?? 0) } > visiblePostsCount
    }

}
