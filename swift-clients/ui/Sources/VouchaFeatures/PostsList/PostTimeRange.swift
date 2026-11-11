/// Finite `time_range` values accepted by post lists. The default selection is `nil` (omitted).
public enum PostTimeRange: String, CaseIterable, Sendable {
    case lastDay = "1d"
    case lastWeek = "1w"
    case last30Days = "1m"
    case lastYear = "1y"
    case allTime = "all"
}
