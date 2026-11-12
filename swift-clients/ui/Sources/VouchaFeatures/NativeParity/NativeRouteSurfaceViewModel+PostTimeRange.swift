import Foundation

extension NativeRouteSurfaceViewModel {
    var showsPostTimeRangePicker: Bool {
        if destination == .postsBrowse || destination == .storiesBrowse { return true }
        guard destination == .userProfile,
              let routeMatch,
              let scope = NativeUserProfileScope(match: routeMatch),
              case .posts = scope
        else { return false }
        return true
    }

    var postTimeRange: PostTimeRange {
        let requested = nativeRouteQueryItems(fromEncodedQuery: routeQuery)["time_range"]
            .flatMap(PostTimeRange.init(rawValue:))
        return requested.flatMap { PostTimeRange.listOptions.contains($0) ? $0 : nil } ?? .last30Days
    }

    /// The ordinary 30-day default is represented by an omitted parameter.
    var postTimeRangeQueryValue: String? {
        postTimeRange == .last30Days ? nil : postTimeRange.rawValue
    }

    func postTimeRangePath(_ range: PostTimeRange) -> String {
        var components = URLComponents()
        components.path = routeMatch?.path ?? "/posts"
        components.percentEncodedQuery = routeQuery
        var items = components.queryItems ?? []
        items.removeAll { $0.name == "time_range" || $0.name == "after" }
        if range != .last30Days {
            items.append(.init(name: "time_range", value: range.rawValue))
        }
        components.queryItems = items.isEmpty ? nil : items
        return components.string ?? components.path
    }
}
