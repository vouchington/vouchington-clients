import Foundation

extension NativeRouteSurfaceViewModel {
    var showsPostTimeRangePicker: Bool {
        if topicPostCollection != nil { return true }
        if destination == .postsBrowse || destination == .storiesBrowse { return true }
        guard destination == .userProfile,
              let routeMatch,
              let scope = NativeUserProfileScope(match: routeMatch),
              case .posts = scope
        else { return false }
        return true
    }

    var postTimeRangeOptions: [PostTimeRange] {
        PostTimeRange.listOptions.filter { !hasHackerNewsReferences || !supportsHackerNewsFilter || $0 != .allTime }
    }

    var boundedHackerNewsTimeRange: PostTimeRange {
        let requested = nativeRouteQueryItems(fromEncodedQuery: routeQuery)["time_range"]
            .flatMap(PostTimeRange.init(rawValue:))
        return requested == nil || requested == .allTime ? .lastYear : requested ?? .lastYear
    }

    var postTimeRange: PostTimeRange {
        let requested = nativeRouteQueryItems(fromEncodedQuery: routeQuery)["time_range"]
            .flatMap(PostTimeRange.init(rawValue:))
        let range = selectedPostTimeRange ?? requested
        if hasHackerNewsReferences, supportsHackerNewsFilter,
           range == nil || range == .allTime { return .lastYear }
        return range ?? .last30Days
    }

    /// Explicitly bound the request: omission disables the producer age filter.
    var postTimeRangeQueryValue: String {
        postTimeRange.rawValue
    }

    func postTimeRangePath(_ range: PostTimeRange) -> String {
        var components = URLComponents()
        components.percentEncodedPath = routeMatch?.path ?? "/posts"
        components.percentEncodedQuery = routeQuery
        var items = components.percentEncodedQueryItems ?? []
        items.removeAll { $0.name == "time_range" || $0.name == "after" || $0.name == "has_hacker_news_references" }
        if hasHackerNewsReferences, supportsHackerNewsFilter {
            items.append(.init(name: "has_hacker_news_references", value: "true"))
        }
        if range != .last30Days || (hasHackerNewsReferences && supportsHackerNewsFilter) {
            items.append(.init(name: "time_range", value: range.rawValue))
        }
        components.percentEncodedQueryItems = items.isEmpty ? nil : items
        return components.string ?? components.path
    }
}
