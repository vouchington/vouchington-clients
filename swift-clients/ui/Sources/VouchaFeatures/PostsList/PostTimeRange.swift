/// Finite `time_range` values accepted by post lists.
/// The default is the last 30 days: `nil` omits the parameter for every sort.
public enum PostTimeRange: String, CaseIterable, Sendable {
    case lastDay = "1d"
    case lastWeek = "1w"
    case last30Days = "1m"
    case lastYear = "1y"
    case allTime = "all"
}
