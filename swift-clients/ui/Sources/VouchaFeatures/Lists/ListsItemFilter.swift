public enum ListsItemFilter: String, CaseIterable, Identifiable, Sendable {
    case all
    case reading
    case watch
    case listen

    public var id: String {
        rawValue
    }

    var mediaType: String? {
        switch self {
        case .all: nil
        case .reading: "article"
        case .watch: "video"
        case .listen: "audio"
        }
    }
}
